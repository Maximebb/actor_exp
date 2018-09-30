using System;
using System.Threading;
using Actor.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StateProviderTests
{
    public class Tests
    {
        const string endpoint = "";
        const string primarykey = "";
        static ILogger logger = NullLogger.Instance;
        IStateProvider<TestIdentity, TestState> stateProvider = TestStateProviderImpl.CreateNew( endpoint, primarykey, logger );

        [Fact]
        public void GetDocumentIsSuccesfull()
        {
            var identity = new TestIdentity();
            var state0 = new TestState() { Val = "Initial" };
            var savedState = stateProvider.SaveStateAsync( CancellationToken.None, identity, state0 ).Result; //blocking on a task for test purposes, should have a timeout
            var readState = stateProvider.GetStateAsync( CancellationToken.None, identity ).Result;
            Assert.True( savedState.ETag == readState.ETag && savedState.Val == readState.Val && savedState.Val != null );
        }

        [Fact]
        public void ConcurrentWriteProducesDirtyWrite()
        {
            var identity = new TestIdentity();
            var state0 = new TestState() { Val = "Initial" };
            var savedState = stateProvider.SaveStateAsync( CancellationToken.None, identity, state0 ).Result;
            var state1 = new TestState() { Val = "FirstModifier" };
            state1.Version = savedState.ETag;
            var state2 = new TestState() { Val = "SecondModifier" };
            state2.Version = savedState.ETag;
            bool isDirtyWrite = false;
            try
            {
                var _1 = stateProvider.SaveStateAsync( CancellationToken.None, identity, state1 ).Result;
                var _2 = stateProvider.SaveStateAsync( CancellationToken.None, identity, state2 ).Result;
            }
            catch( AggregateException ex )
            {
                if ( ex.InnerException is DirtyWriteExn )
                    isDirtyWrite = true;
                else
                    isDirtyWrite = false;
            }
            catch
            {
                isDirtyWrite = false;
            }
            var finalState = stateProvider.GetStateAsync( CancellationToken.None, identity ).Result;
            Assert.True( isDirtyWrite && finalState.Val == "FirstModifier" );
        }
    }
}
