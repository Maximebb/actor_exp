using System;
using System.Collections.Generic;
using System.Text;
using Actor.Common;
using Microsoft.Extensions.Logging;
using StatelessActor.Infrastructure;

namespace StateProviderTests
{
    public class TestState : VersionableStateBase
    {

        public TestState(string etag) : base()
        {
            this.ETag = etag;
        }
    }

    public class TestIdentity : IComparable
    {
        public Guid Id { get; set; }

        public int CompareTo( object obj )
        {
            throw new NotImplementedException();
        }
    }

    public class TestStateProviderImpl : ConnectedStateProviderBase<TestIdentity, TestState>
    {
        public const string BaseAddress = "Test";

        private TestStateProviderImpl( string endpoint, string primarykey, ILogger logger ) : base( endpoint, primarykey, logger )
        {

        }

        public override Uri GetUriForId( TestIdentity id )
        {
            return new Uri($"{BaseAddress}{id.Id.ToString()}");
        }

        public static IStateProvider<TestIdentity, TestState> CreateNew( string endpoint, string primarykey, ILogger logger )
        {
            return new TestStateProviderImpl( endpoint, primarykey, logger );
        }
    }
}
