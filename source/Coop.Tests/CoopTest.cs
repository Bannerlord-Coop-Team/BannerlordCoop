using Common.Messaging;
using Common.Network;
using Coop.Tests.Stubs;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class CoopTest
    {
        public readonly StubMessageBroker StubMessageBroker;
        public readonly StubNetworkMessageBroker StubNetworkMessageBroker;
        public readonly ITestOutputHelper Output;

        public CoopTest(ITestOutputHelper output)
        {
            Output = output;
            StubNetworkMessageBroker = new StubNetworkMessageBroker();
            StubMessageBroker = StubNetworkMessageBroker;
        }
    }
}
