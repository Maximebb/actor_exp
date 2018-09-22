using System;
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
            //boilerplate
        }
    }
}
