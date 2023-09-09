using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CreateCharacterStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        private MockMessageBroker MockMessageBroker => serverComponent.MockMessageBroker;
        private MockNetwork MockNetwork => serverComponent.MockNetwork;

        public CreateCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<MockNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void TransferCharacter_TransitionState_TransferCharacterState()
        {
            connectionLogic.SetState<CreateCharacterState>();

            connectionLogic.TransferSave();

            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            connectionLogic.SetState<CreateCharacterState>();

            connectionLogic.CreateCharacter();
            connectionLogic.Load();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferedHero_Valid()
        {
            // Arrange
            var currentState = connectionLogic.SetState<CreateCharacterState>();

            // Act
            var payload = new MessagePayload<NetworkTransferedHero>(
                playerPeer, new NetworkTransferedHero(null, Array.Empty<byte>()));
            currentState.PlayerTransferedHeroHandler(payload);

            // Assert
            Assert.Single(MockMessageBroker.PublishedMessages);
            Assert.IsType<RegisterNewPlayerHero>(MockMessageBroker.PublishedMessages[0]);
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NewPlayerHeroRegistered_Valid()
        {
            // Arrange
            var currentState = connectionLogic.SetState<CreateCharacterState>();

            // Act
            var payload = new MessagePayload<NewPlayerHeroRegistered>(
                playerPeer, new NewPlayerHeroRegistered(playerPeer, default));
            currentState.PlayerHeroRegisteredHandler(payload);

            // Assert
            Assert.Equal(2, MockNetwork.GetPeerMessages(playerPeer).Count());
            var message = MockNetwork.GetPeerMessages(playerPeer).First();
            Assert.IsType<NetworkPlayerData>(message);

            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }
    }
}
