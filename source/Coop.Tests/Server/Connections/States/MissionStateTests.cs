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
    public class MissionStateTests : CoopTest
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPlayer;
        public MissionStateTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPlayer = MockNetwork.CreatePeer();

            connectionLogic = new ConnectionLogic(playerPeer, MockMessageBroker, MockNetwork);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            connectionLogic.State = new CampaignState(connectionLogic);

            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            connectionLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void EnterCampaignMethod_TransitionState_CampaignState()
        {
            // Arrange
            connectionLogic.State = new MissionState(connectionLogic);

            // Act
            connectionLogic.EnterCampaign();

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.State = new MissionState(connectionLogic);

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
            var currentState = new MissionState(connectionLogic);
            connectionLogic.State = currentState;

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
            var currentState = new MissionState(connectionLogic);
            connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                differentPlayer, new NetworkPlayerCampaignEntered());
            currentState.PlayerTransitionsCampaignHandler(payload);

            // Assert
            Assert.IsType<MissionState>(connectionLogic.State);
        }
    }
}
