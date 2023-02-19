using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CharacterCreationStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public CharacterCreationStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, NetworkMessageBroker);
            clientLogic.State = new CharacterCreationState(clientLogic);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            clientLogic.State.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMainMenu();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void EnterMainMenu_Transitions_MainMenuState()
        {
            MessageBroker.Publish(this, new MainMenuEntered());

            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Transitions_ReceivingSavedDataState()
        {
            MessageBroker.Publish(this, new CharacterCreationFinished());

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_LoadGameSave()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<LoadGameSave>((payload) =>
            {
                isEventPublished = true;
            });

            MessageBroker.Publish(this, new CharacterCreationFinished());

            Assert.True(isEventPublished);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.Disconnect();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Connect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }
    }
}
