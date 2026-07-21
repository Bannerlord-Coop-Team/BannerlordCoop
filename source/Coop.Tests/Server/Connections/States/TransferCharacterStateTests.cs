using Autofac;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.MapEvents.BattleSize;
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
            connectionLogic = container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));
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
        public void EnteringState_SendsSaveDataChunks_ToJoiningPeerOnly()
        {
            // Arrange — the save interface returns a known payload.
            byte[] data = new byte[] { 1, 2, 3 };
            string campaignId = "12345";
            var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
            saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(true, data, campaignId));
            var battleSizeProvider = serverComponent.Container.Resolve<Mock<IServerBattleSizeProvider>>();
            battleSizeProvider.SetupGet(m => m.BattleSize).Returns(800);

            // Act — entering the state packages the save and sends it to the joining peer.
            connectionLogic.SetState<TransferSaveState>();

            // Assert — exactly one save chunk for this tiny save, carrying the deflate-compressed
            // save data to the joining peer.
            var packet = Assert.Single(serverComponent.TestNetwork.GetPeerPacketsFromType<GameSaveDataChunkPacket>(playerPeer));
            Assert.Equal(data, SaveDataCompression.Decompress(packet.ChunkData));
            Assert.Equal(campaignId, packet.CampaignID);
            Assert.Equal(0, packet.ChunkIndex);
            Assert.Equal(1, packet.ChunkCount);
            Assert.Equal(800, packet.BattleSize);
            serverComponent.Container.Resolve<Mock<ITimeControlInterface>>()
                .Verify(m => m.ServerSetTimeControl(It.IsAny<TimeControlEnum>()), Times.Never);

            // The directed save is not sent to any other peer.
            var otherPeerGotSave =
                serverComponent.TestNetwork.SentPackets.TryGetValue(differentPeer.Id, out var packets) &&
                packets.OfType<GameSaveDataChunkPacket>().Any();
            Assert.False(otherPeerGotSave);
        }

        [Fact]
        public void EnteringState_SplitsLargeSaveData_IntoChunks()
        {
            // Arrange
            byte[] data = new byte[(GameSaveDataChunkPacket.ChunkSize * 2) + 17];
            new System.Random(42).NextBytes(data);
            var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
            saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(true, data, "12345"));
            var battleSizeProvider = serverComponent.Container.Resolve<Mock<IServerBattleSizeProvider>>();
            battleSizeProvider.SetupGet(m => m.BattleSize).Returns(800);

            // Act
            connectionLogic.SetState<TransferSaveState>();

            // Assert
            var packets = serverComponent.TestNetwork.GetPeerPacketsFromType<GameSaveDataChunkPacket>(playerPeer).ToArray();
            Assert.True(packets.Length > 1);
            Assert.Equal(Enumerable.Range(0, packets.Length), packets.Select(x => x.ChunkIndex));
            Assert.All(packets, x => Assert.Equal(packets.Length, x.ChunkCount));
            Assert.All(packets, x => Assert.True(x.ChunkData.Length <= GameSaveDataChunkPacket.ChunkSize));
            Assert.Equal("12345", packets[0].CampaignID);
            Assert.Equal(800, packets[0].BattleSize);
            Assert.All(packets.Skip(1), x => Assert.Null(x.CampaignID));
            Assert.All(packets.Skip(1), x => Assert.Equal(0, x.BattleSize));
        }
    }
}
