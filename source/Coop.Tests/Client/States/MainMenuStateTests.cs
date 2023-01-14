using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MainMenuStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public MainMenuStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, messageBroker);
        }

        [Fact]
        public void Ctor_SubscribesNetworkConnected()
        {
            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(1, subscriberCount);
        }

        [Fact]
        public void Connect_CharacterNotCreated_EnterCharacterCreation()
        {
            messageBroker.Publish(this, new NetworkConnected(false));

            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }

        [Fact]
        public void Connect_CharacterCreated_ReceivingSavedDataState()
        {
            messageBroker.Publish(this, new NetworkConnected(true));

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.Disconnect();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            clientLogic.Dispose();

            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(0, subscriberCount);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Disconnect();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterMainMenu();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<MainMenuState>(clientLogic.State);
        }
    }
}
