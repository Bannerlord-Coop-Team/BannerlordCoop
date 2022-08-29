using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Moq;
using Coop.Core.Debugging.Logger;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MainMenuStateTests : CoopTest
    {
        public MainMenuStateTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Ctor_SubscribesNetworkConnect()
        {
            var mockCoopClient = new Mock<ICoopClient>();
            var clientLogic = new ClientLogic(new Mock<ILogger>().Object, mockCoopClient.Object, messageBroker);
            _ = new MainMenuState(clientLogic, messageBroker);

            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(1, subscriberCount);
        }

        [Fact]
        public void Connect_CharacterNotCreated_EnterCharacterCreation()
        {
            var mockCoopClient = new Mock<ICoopClient>();
            mockCoopClient.Setup(s => s.Start());

            var clientLogic = new ClientLogic(new Mock<ILogger>().Object, mockCoopClient.Object, messageBroker);
            var currentState = new MainMenuState(clientLogic, messageBroker);

            clientLogic.Start();

            messageBroker.Publish(this, new NetworkConnected(false));

            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }

        [Fact]
        public void Connect_CharacterCreated_ReceivingSavedDataState()
        {
            var mockCoopClient = new Mock<ICoopClient>();
            mockCoopClient.Setup(s => s.Start());

            var clientLogic = new ClientLogic(new Mock<ILogger>().Object, mockCoopClient.Object, messageBroker);
            var currentState = new MainMenuState(clientLogic, messageBroker);

            clientLogic.Start();

            messageBroker.Publish(this, new NetworkConnected(true));

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            var mockCoopClient = new Mock<ICoopClient>();
            mockCoopClient.Setup(s => s.Start());

            var clientLogic = new ClientLogic(new Mock<ILogger>().Object, mockCoopClient.Object, messageBroker);
            var currentState = new MainMenuState(clientLogic, messageBroker);

            currentState.Dispose();

            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(0, subscriberCount);
        }
    }
}
