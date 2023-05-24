using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using LiteNetLib;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CampaignStateTests : CoopTest
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        public CampaignStateTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPeer = MockNetwork.CreatePeer();
            connectionLogic = new ConnectionLogic(playerPeer, MockMessageBroker, MockNetwork);
            differentPeer.SetId(playerPeer.Id + 1);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            connectionLogic.State = new CampaignState(connectionLogic);

            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            connectionLogic.State.Dispose();

            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void EnterMissionMethod_TransitionState_MissionState()
        {
            connectionLogic.State = new CampaignState(connectionLogic);

            connectionLogic.EnterMission();

            Assert.IsType<MissionState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            connectionLogic.State = new CampaignState(connectionLogic);

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
            CampaignState currentState = new CampaignState(connectionLogic);
            connectionLogic.State = currentState;

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
            CampaignState currentState = new CampaignState(connectionLogic);
            connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<NetworkPlayerMissionEntered>(
                differentPeer, new NetworkPlayerMissionEntered());
            currentState.PlayerMissionEnteredHandler(payload);

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }
    }
}
