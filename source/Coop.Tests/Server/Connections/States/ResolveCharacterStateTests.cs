using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ResolveCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        public ResolveCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPeer = MockNetwork.CreatePeer();
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
        public void CreateCharacterMethod_TransitionState_CreateCharacterState()
        {
            // Arrange
            connectionLogic.State = new ResolveCharacterState(connectionLogic);

            // Act
            connectionLogic.CreateCharacter();

            // Assert
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void TransferSaveMethod_TransitionState_TransferSaveState()
        {
            // Arrange
            connectionLogic.State = new ResolveCharacterState(connectionLogic);

            // Act
            connectionLogic.TransferSave();

            // Assert
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.State = new ResolveCharacterState(connectionLogic);

            // Act
            connectionLogic.Load();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<ResolveCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkClientValidate_ValidPlayerId()
        {
            // Arrange
            var currentState = new ResolveCharacterState(connectionLogic);
            connectionLogic.State = currentState;

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<ResolveDebugHero>(message);

            var castedMessage = (ResolveDebugHero)message;
            Assert.Equal(playerId, castedMessage.PlayerId);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            // Arrange
            var currentState = new ResolveCharacterState(connectionLogic);
            connectionLogic.State = currentState;

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                differentPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            Assert.Empty(MockMessageBroker.PublishedMessages);
        }

        [Fact]
        public void ResolveHero_Valid()
        {
            // Arrange
            var currentState = new ResolveCharacterState(connectionLogic);
            connectionLogic.State = currentState;

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<HeroResolved>(
                playerPeer, new HeroResolved(playerId));
            currentState.ResolveHeroHandler(payload);

            // Assert
            Assert.Equal(2, MockNetwork.GetPeerMessages(playerPeer).Count());
            var message = MockNetwork.GetPeerMessages(playerPeer).First();
            Assert.IsType<NetworkClientValidated>(message);

            var castedMessage = (NetworkClientValidated)message;
            Assert.Equal(playerId, castedMessage.HeroId);
            Assert.True(castedMessage.HeroExists);

            Assert.Single(MockNetwork.GetPeerMessages(differentPeer));
        }

        [Fact]
        public void HeroNotFound_Valid()
        {
            // Arrange
            var currentState = new ResolveCharacterState(connectionLogic);
            connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<ResolveHeroNotFound>(
                playerPeer, new ResolveHeroNotFound());
            currentState.HeroNotFoundHandler(payload);

            // Assert
            var message = Assert.Single(MockNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkClientValidated>(message);

            var castedMessage = (NetworkClientValidated)message;
            Assert.Equal(string.Empty, castedMessage.HeroId);
            Assert.False(castedMessage.HeroExists);

            Assert.False(MockNetwork.SentNetworkMessages.ContainsKey(differentPeer.Id));
        }
    }
}
