using System;
using System.Collections.Generic;
using System.Text;
using Actor.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StatelessActor.Infrastructure;

namespace StateProviderTests
{
    /// <summary>
    /// State is basically just the version with a text
    /// </summary>
    public class TestState : VersionableStateBase
    {
        [JsonProperty(PropertyName = "Val")]
        public string Val { get; set; }

        public TestState()
        {
        }
    }

    /// <summary>
    /// Simple wrapper for a guid
    /// </summary>
    public class TestIdentity : IdentityBase, IComparable
    {
        public TestIdentity()
        {
            this.Id = Guid.NewGuid();
        }

        public int CompareTo( object obj )
        {
            if ( obj is TestIdentity id2 )
            {
                return Id.CompareTo( id2.Id );
            }
            else
            {
                throw new ArgumentException( "Cannot compare to object not of type TestIdentity" );
            }
        }

        public override string ToString()
        {
            return Id.ToString();
        }
    }

    /// <summary>
    /// Test implementation of the state provider
    /// </summary>
    public class TestStateProviderImpl : ConnectedStateProviderBase<TestIdentity, TestState>
    {
        public const string DatabaseId = "States";
        public const string BaseContainer = "Test";

        private TestStateProviderImpl( string endpoint, string primarykey, ILogger logger )
            : base( endpoint, primarykey, DatabaseId, BaseContainer, logger ) { }
        
        public static IStateProvider<TestIdentity, TestState> CreateNew( string endpoint, string primarykey, ILogger logger )
        {
            return new TestStateProviderImpl( endpoint, primarykey, logger );
        }

        public override string DocumentId( TestIdentity id )
        {
            return id.Id.ToString();
        }
    }
}
