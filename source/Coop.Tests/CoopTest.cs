using Common.Messaging;
using Common.Network;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class CoopTest
    {
        public readonly MockMessageBroker MockMessageBroker;
        public readonly MockNetwork MockNetwork;
        public readonly ITestOutputHelper Output;

        public CoopTest(ITestOutputHelper output)
        {
            Output = output;
            MockMessageBroker = new MockMessageBroker();
            MockNetwork = new MockNetwork();
        }
    }
}
