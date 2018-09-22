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
    public abstract class VersionableStateBase : IVersionableState
    {
        public string ETag { get; set; }
    }

    public abstract class ConnectedStateProviderBase<identity, state> : IStateProvider<identity, state>
        where state : VersionableStateBase
    {
        private DocumentClient m_client;
        private ILogger m_logger;

        public ConnectedStateProviderBase( string endpoint, string primarykey, ILogger logger )
        {
            m_logger = logger;
            m_client = new DocumentClient( new Uri( endpoint ), primarykey );
        }

        #region Get
        public Task<state> GetStateAsync( CancellationToken token, identity id )
        {
            return GetStateAsyncInternal( token, id );
        }

        private async Task<state> GetStateAsyncInternal( CancellationToken token, identity id )
        {
            var s = default(state);
            var uri = GetUriForId( id );
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
            return await SaveStateAsyncInternal( token, id, st );
        }

        private async Task<state> SaveStateAsyncInternal( CancellationToken token, identity id, state st )
        {
            var s = st;
            var uri = GetUriForId( id );
            try
            {
                var options = new RequestOptions() { AccessCondition = new AccessCondition() { Type = AccessConditionType.IfMatch, Condition = st.ETag } };
                var result = await m_client.UpsertDocumentAsync( uri, st, options );
                s = ( state ) ( dynamic ) result;
                s.ETag = result.Resource.ETag;
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
        public abstract Uri GetUriForId( identity id );

    }
}
