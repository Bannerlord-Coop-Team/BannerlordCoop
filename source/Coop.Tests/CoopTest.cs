using Common.Messaging;
using Common.Network;
using Coop.Tests.Stubs;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class CoopTest
    {
        public readonly IMessageBroker MessageBroker;
        public readonly INetworkMessageBroker NetworkMessageBroker;
        public readonly ITestOutputHelper Output;

        public CoopTest(ITestOutputHelper output)
        {
            Output = output;
            MessageBroker = new MessageBrokerStub();
            NetworkMessageBroker = new NetworkMessageBrokerStub();
        }
    }
}
