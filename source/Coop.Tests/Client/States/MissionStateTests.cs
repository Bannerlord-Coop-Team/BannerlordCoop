using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MissionStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;

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
            var message = Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<EnterCampaignState>(message);
        }

        [Fact]
        public void EnterMainMenu_GoesToMainMenu()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();
            var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
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
        public void Disconnect_GoesToMainMenu()
        {
            // Arrange
            var missionState = clientLogic.SetState<MissionState>();
            var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

            // Act
            clientLogic.Disconnect();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
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
