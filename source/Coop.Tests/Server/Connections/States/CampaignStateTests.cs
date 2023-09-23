using Autofac;
using Common.Messaging;
using Coop.Core;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using LiteNetLib;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CampaignStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        private MockMessageBroker MockMessageBroker => serverComponent.MockMessageBroker;
        private MockNetwork MockNetwork => serverComponent.MockNetwork;

        public CampaignStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<MockNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<IConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));

            differentPeer.SetId(playerPeer.Id + 1);
        }

        [Fact]
        public void EnterMissionMethod_TransitionState_MissionState()
        {
            connectionLogic.SetState<CampaignState>();

            connectionLogic.EnterMission();

            Assert.IsType<MissionState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            connectionLogic.SetState<CampaignState>();

            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.Load();
            connectionLogic.EnterCampaign();

            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerMissionEntered_ValidPlayerId()
        {
            // Arrange
            CampaignState currentState = connectionLogic.SetState<CampaignState>();

            // Act
            var payload = new MessagePayload<NetworkPlayerMissionEntered>(
                playerPeer, new NetworkPlayerMissionEntered());
            currentState.PlayerMissionEnteredHandler(payload);

            // Assert
            Assert.IsType<MissionState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerMissionEntered_InvalidPlayerId()
        {
            // Arrange
            CampaignState currentState = connectionLogic.SetState<CampaignState>();

            // Act
            var payload = new MessagePayload<NetworkPlayerMissionEntered>(
                differentPeer, new NetworkPlayerMissionEntered());
            currentState.PlayerMissionEnteredHandler(payload);

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }
    }
}
