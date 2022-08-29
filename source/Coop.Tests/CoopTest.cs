using Common.Messaging;
using Coop.Tests.Stubs;
using Moq;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class CoopTest
    {
        public readonly Mock<IMessageBroker> mockMessageBroker;
        public readonly MessageBrokerStub messageBroker;
        public readonly ITestOutputHelper output;

        public CoopTest(ITestOutputHelper output)
        {
            this.output = output;
            messageBroker = new MessageBrokerStub();
            mockMessageBroker = new Mock<IMessageBroker>();
        }
    }
}
