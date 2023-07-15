using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CreateCharacterStateTests : CoopTest
    {
        private readonly IConnectionLogic _connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPlayer;
        public CreateCharacterStateTests(ITestOutputHelper output) : base(output)
        {
            playerPeer = MockNetwork.CreatePeer();
            differentPlayer = MockNetwork.CreatePeer();
            _connectionLogic = new ConnectionLogic(playerPeer, MockMessageBroker, MockNetwork);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            // Arrange
            _connectionLogic.State = new CampaignState(_connectionLogic);

            Assert.NotEmpty(MockMessageBroker.Subscriptions);

            // Act
            _connectionLogic.State.Dispose();

            // Assert
            Assert.Empty(MockMessageBroker.Subscriptions);
        }

        [Fact]
        public void TransferCharacter_TransitionState_TransferCharacterState()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            _connectionLogic.TransferSave();

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            _connectionLogic.State = new CreateCharacterState(_connectionLogic);

            _connectionLogic.CreateCharacter();
            _connectionLogic.Load();
            _connectionLogic.EnterCampaign();
            _connectionLogic.EnterMission();

            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferedHero_Valid()
        {
            // Arrange
            var currentState = new CreateCharacterState(_connectionLogic);
            _connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<NetworkTransferedHero>(
                playerPeer, new NetworkTransferedHero(null, Array.Empty<byte>()));
            currentState.PlayerTransferedHeroHandler(payload);

            // Assert
            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<RegisterNewPlayerHero>(MockMessageBroker.PublishedMessages[0]);
            Assert.IsType<CreateCharacterState>(_connectionLogic.State);
        }

        [Fact]
        public void NewPlayerHeroRegistered_Valid()
        {
            // Arrange
            var currentState = new CreateCharacterState(_connectionLogic);
            _connectionLogic.State = currentState;

            // Act
            var payload = new MessagePayload<NewPlayerHeroRegistered>(
                playerPeer, new NewPlayerHeroRegistered(default));
            currentState.PlayerHeroRegisteredHandler(payload);

            // Assert
            Assert.Equal(2, MockNetwork.GetPeerMessages(playerPeer).Count());
            var message = MockNetwork.GetPeerMessages(playerPeer).First();
            Assert.IsType<NetworkPlayerData>(message);

            Assert.IsType<TransferSaveState>(_connectionLogic.State);
        }
    }
}
