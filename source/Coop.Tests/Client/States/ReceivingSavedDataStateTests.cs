using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Debugging.Logger;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ReceivingSavedDataStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public ReceivingSavedDataStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(new Mock<ILogger>().Object, mockCoopClient.Object, messageBroker);
            clientLogic.State = new ReceivingSavedDataState(clientLogic, messageBroker);
        }

        [Fact]
        public void Ctor_Subscribes()
        {
            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(2, subscriberCount);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMainMenu();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void EnterMainMenu_Transitions_MainMenuState()
        {
            messageBroker.Publish(this, new MainMenuEntered());

            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Transitions_ValidateModuleState()
        {
            messageBroker.Publish(this, new GameSaveLoaded());

            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_ValidateModule()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<ValidateModule>((payload) =>
            {
                isEventPublished = true;
            });

            messageBroker.Publish(this, new GameSaveLoaded());

            Assert.True(isEventPublished);
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
            clientLogic.Connect();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }
    }
}
