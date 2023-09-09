using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ResolveCharacterStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        private MockMessageBroker MockMessageBroker => serverComponent.MockMessageBroker;
        private MockNetwork MockNetwork => serverComponent.MockNetwork;

        public ResolveCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<MockNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void CreateCharacterMethod_TransitionState_CreateCharacterState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.CreateCharacter();

            // Assert
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void TransferSaveMethod_TransitionState_TransferSaveState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.TransferSave();

            // Assert
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

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
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            var message = Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<ResolveHero>(message);

            var castedMessage = (ResolveHero)message;
            Assert.Equal(playerId, castedMessage.PlayerId);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

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
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

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
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

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
