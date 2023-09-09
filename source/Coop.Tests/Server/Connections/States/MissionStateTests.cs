using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using LiteNetLib;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class MissionStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        private MockMessageBroker MockMessageBroker => serverComponent.MockMessageBroker;
        private MockNetwork MockNetwork => serverComponent.MockNetwork;

        public MissionStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<MockNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void EnterCampaignMethod_TransitionState_CampaignState()
        {
            // Arrange
            connectionLogic.SetState<MissionState>();

            // Act
            connectionLogic.EnterCampaign();

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<MissionState>();

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.Load();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<MissionState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_ValidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<MissionState>();

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                playerPeer, new NetworkPlayerCampaignEntered());
            currentState.PlayerTransitionsCampaignHandler(payload);

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<MissionState>();

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                differentPeer, new NetworkPlayerCampaignEntered());
            currentState.PlayerTransitionsCampaignHandler(payload);

            // Assert
            Assert.IsType<MissionState>(connectionLogic.State);
        }
    }
}
