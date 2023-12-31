using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CampaignStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly ClientTestComponent clientComponent;

        private MockMessageBroker MockMessageBroker => clientComponent.MockMessageBroker;
        private MockNetwork MockNetwork => clientComponent.MockNetwork;
        public CampaignStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;
            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void EnterMissionState_Publishes_EnterMissionState()
        {
            // Arrange
            var campaignState = clientLogic.SetState<CampaignState>();

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
            var campaignState = clientLogic.SetState<CampaignState>();

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
            var campaignState = clientLogic.SetState<CampaignState>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            // Arrange
            var campaignState = clientLogic.SetState<CampaignState>();

            // Act
            clientLogic.Disconnect();

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<EnterMainMenu>(message);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            var campaignState = clientLogic.SetState<CampaignState>();

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
