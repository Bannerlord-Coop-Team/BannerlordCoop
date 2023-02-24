using Common.Messaging;
using Common.Network;
using Coop.Tests.Stubs;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class CoopTest
    {
        public readonly MessageBrokerStub MessageBroker;
        public readonly NetworkMessageBrokerStub NetworkMessageBroker;
        public readonly ITestOutputHelper Output;

        public CoopTest(ITestOutputHelper output)
        {
            Output = output;
            NetworkMessageBroker = new NetworkMessageBrokerStub();
            MessageBroker = NetworkMessageBroker;
        }
    }
}
