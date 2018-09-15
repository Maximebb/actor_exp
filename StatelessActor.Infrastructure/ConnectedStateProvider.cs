using System;
using Microsoft.Azure.Documents.Client;

namespace StatelessActor.Infrastructure
{
    public class ConnectedStateProvider
    {
        private const string EndPoint = "";
        private const string PrimaryKey = "";
        private DocumentClient m_client;

        public ConnectedStateProvider()
        {
            m_client = new DocumentClient( new Uri( EndPoint ), PrimaryKey );
        }

        
    }
}
