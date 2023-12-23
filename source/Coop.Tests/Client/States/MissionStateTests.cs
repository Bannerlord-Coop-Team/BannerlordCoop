using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MissionStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;

        private MockMessageBroker MockMessageBroker => clientComponent.MockMessageBroker;
        private MockNetwork MockNetwork => clientComponent.MockNetwork;

        public MissionStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void EnterCampaignState_Transitions_CampaignState()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();

            var payload = new MessagePayload<CampaignStateEntered>(
                this, new CampaignStateEntered());

            // Act
            missionState.Handle_CampaignStateEntered(payload);

            // Assert
            Assert.IsType<CampaignState>(clientLogic.State);
        }

        [Fact]
        public void EnterCampaignState_Publishes_EnterCampaignState()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();

            // Act
            clientLogic.EnterCampaignState();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterCampaignState>(message);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void MainMenuEntered_Transitions_MainMenuState()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            missionState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();

            // Act
            clientLogic.Connect();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.EnterMainMenu();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<MissionState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<MissionState>(clientLogic.State);
        }
    }
}
