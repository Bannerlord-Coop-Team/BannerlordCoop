using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class LoadingStateTests : CoopTest
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;

        public LoadingStateTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPeer = MockNetwork.CreatePeer();

            connectionLogic = new ConnectionLogic(playerPeer, MockMessageBroker, MockNetwork);
            differentPeer.SetId(playerPeer.Id + 1);
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
            connectionLogic.State = new LoadingState(connectionLogic);

            // Act
            connectionLogic.EnterCampaign();

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.State = new LoadingState(connectionLogic);

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.Load();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_ValidPlayerId()
        {
            // Arrange
            var currentState = new LoadingState(connectionLogic);
            connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                playerPeer, new NetworkPlayerCampaignEntered());
            currentState.PlayerCampaignEnteredHandler(payload);


            // Assert
            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<PlayerCampaignEntered>(MockMessageBroker.PublishedMessages.First());

            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_InvalidPlayerId()
        {
            // Arrange
            var currentState = new LoadingState(connectionLogic);
            connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                differentPeer, new NetworkPlayerCampaignEntered());
            currentState.PlayerCampaignEnteredHandler(payload);


            // Assert
            Assert.Empty(MockMessageBroker.PublishedMessages);

            Assert.IsType<LoadingState>(connectionLogic.State);
        }
    }
}
