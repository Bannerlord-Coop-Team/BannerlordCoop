using Autofac;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class TransferCharacterStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public TransferCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            // Arrange
            connectionLogic.SetState<TransferSaveState>();

            // Act
            connectionLogic.Load();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<TransferSaveState>();

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void EnteringState_SendsSaveDataPacket_ToJoiningPeerOnly()
        {
            // Arrange — the save interface returns a known payload.
            byte[] data = new byte[] { 1, 2, 3 };
            string campaignId = "12345";
            var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
            saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(true, data, campaignId));

            // Act — entering the state packages the save and sends it to the joining peer.
            connectionLogic.SetState<TransferSaveState>();

            // Assert — exactly one save packet, carrying the save data, to the joining peer.
            var packet = Assert.Single(serverComponent.TestNetwork.GetPeerPacketsFromType<GameSaveDataPacket>(playerPeer));
            Assert.Equal(data, packet.GameSaveData);
            Assert.Equal(campaignId, packet.CampaignID);

            // The directed save is not sent to any other peer.
            var otherPeerGotSave =
                serverComponent.TestNetwork.SentPackets.TryGetValue(differentPeer.Id, out var packets) &&
                packets.OfType<GameSaveDataPacket>().Any();
            Assert.False(otherPeerGotSave);
        }

        [Fact]
        public void EnteringState_SendsThePlayerRegistry_WithTheSave()
        {
            // Arrange — a player is already registered on the server.
            var existingPlayer = new Player("other", "hero1", "party1", "clan1", "char1");
            var playerManagerMock = serverComponent.Container.Resolve<Mock<IPlayerManager>>();
            playerManagerMock.Setup(m => m.Players).Returns(new[] { existingPlayer });

            var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
            saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(true, new byte[] { 1 }, "12345"));

            // Act
            connectionLogic.SetState<TransferSaveState>();

            // Assert — the joining peer receives the registry snapshot; its heroes are inside
            // the save, so only the records travel.
            var message = Assert.Single(
                serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkExistingPlayers>(playerPeer));
            Assert.Equal(existingPlayer, Assert.Single(message.Players));
        }
    }
}
