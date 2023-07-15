using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Messages;
using Moq;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using GameInterface.Services.GameState.Messages;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Coop.Tests.Client.States
{
    public class CampaignStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public CampaignStateTests(ITestOutputHelper output) : base(output)
        {
            clientLogic = ServiceProvider.GetService<IClientLogic>()!;
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            clientLogic.State = new CampaignState(clientLogic);
            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            clientLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void NetworkDisableTimeControls_Publishes_PauseAndDisableGameTimeControls()
        {
            // Arrange
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

            var payload = new MessagePayload<NetworkDisableTimeControls>(
                this, new NetworkDisableTimeControls());

            // Act
            campaignState.Handle_NetworkDisableTimeControls(payload);

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<PauseAndDisableGameTimeControls>(message);
        }

        [Fact]
        public void EnterMissionState_Publishes_EnterMissionState()
        {
            // Arrange
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

            // Act
            clientLogic.EnterMissionState();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMissionState>(message);
        }

        [Fact]
        public void MissionStateEntered_Transitions_MissionState()
        {
            // Arrange
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

            var payload = new MessagePayload<MissionStateEntered>(
                this, new MissionStateEntered());

            // Act
            campaignState.Handle_MissionStateEntered(payload);

            // Assert
            Assert.IsType<MissionState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            // Arrange
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

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
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            campaignState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            var campaignState = new CampaignState(clientLogic);
            clientLogic.State = campaignState;

            clientLogic.Connect();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<CampaignState>(clientLogic.State);
        }
    }
}
