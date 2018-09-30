using System;
using Microsoft.Azure.Documents.Client;
using Actor.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents;
using System.Net;

namespace StatelessActor.Infrastructure
{
    public abstract class VersionableStateBase : Document, IVersionableState
    {
        public string Version { get; set; }
    }

    public abstract class IdentityBase
    {
        public Guid Id { get; set; }
    }

    public abstract class ConnectedStateProviderBase<identity, state> : IStateProvider<identity, state>
        where state : VersionableStateBase
        where identity : IdentityBase
    {
        private DocumentClient m_client;
        private ILogger m_logger;
        private string m_dbId;
        private string m_baseCollection;
        private Task m_initialized;

        public ConnectedStateProviderBase( string endpoint, string primarykey, string dbId, string baseCollection, ILogger logger )
        {
            m_logger = logger;
            m_baseCollection = baseCollection;
            m_dbId = dbId;
            m_client = new DocumentClient( new Uri( endpoint ), primarykey );
            m_initialized = Task.Run( async () =>
            {
                var db = new Database() { Id = m_dbId };
                var options = new RequestOptions { OfferThroughput = 400 };
                var coll = new DocumentCollection() { Id = m_baseCollection };
                await m_client.OpenAsync();
                var createdDb = await m_client.CreateDatabaseIfNotExistsAsync( db );
                await m_client.CreateDocumentCollectionIfNotExistsAsync( createdDb.Resource.SelfLink, coll, options );
            } );
        }

        #region Get
        public async Task<state> GetStateAsync( CancellationToken token, identity id )
        {
            await m_initialized;
            return await GetStateAsyncInternal( token, id );
        }

        private async Task<state> GetStateAsyncInternal( CancellationToken token, identity id )
        {
            var s = default(state);
            var uri = GetDocumentUriForId( id );
            try
            {
                s = (state) (dynamic) await FetchDocumentAtUri( token, uri );
            }
            catch ( DocumentClientException ex )
            {
                if ( ex.StatusCode != HttpStatusCode.NotFound )
                {
                    m_logger.LogDebug( $"Document did not exist at {{{uri}}}" );
                    return s;
                }
                else
                {
                    m_logger.LogError( $"Could not retrieve state. ex: {ex.Message}" );
                    throw;
                }
            }
            return s;
        }

        private async Task<Document> FetchDocumentAtUri( CancellationToken token, Uri uri )
        {
            /// Somehow documentation shows an overload to inclue a cancellation token, but it's not in the dll.
            /// Gotta find a way to cancel the call in case of teardown...
            var result = await m_client.ReadDocumentAsync( uri );
            return result.Resource;
        }
        #endregion

        #region Save
        public async Task<state> SaveStateAsync( CancellationToken token, identity id, state st )
        {
            await m_initialized;
            return await SaveStateAsyncInternal( token, id, st );
        }

        private async Task<state> SaveStateAsyncInternal( CancellationToken token, identity id, state st )
        {
            st.Id = id.Id.ToString();
            var uri = GetDocumentCollectionUri();
            try
            {
                var options = new RequestOptions() { AccessCondition = new AccessCondition() { Type = AccessConditionType.IfMatch, Condition = st.Version } };
                var result = await m_client.UpsertDocumentAsync( uri, st, options );
                state s = ( dynamic ) result.Resource;
                s.Version = result.Resource.ETag;
                return s;
            }
            catch ( DocumentClientException ex )
            {
                if( ex.StatusCode == HttpStatusCode.PreconditionFailed )
                {
                    m_logger.LogInformation( "Precondition failed." );
                    throw new DirtyWriteExn();
                }
                throw;
            }
        }
        #endregion


        /// <summary>
        /// Implement this method in order to define identity location
        /// </summary>
        /// <param name="id">Identity of the state document</param>
        /// <returns></returns>
        public abstract string DocumentId( identity id ); 

        internal Uri GetDocumentUriForId( identity id )
        {
            return UriFactory.CreateDocumentUri( m_dbId, m_baseCollection, DocumentId( id ) );
        }

        internal Uri GetDocumentCollectionUri()
        {
            return UriFactory.CreateDocumentCollectionUri( m_dbId, m_baseCollection );
        }

    }
}
