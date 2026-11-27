using Autofac;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.Save.Messages;
using Coop.Tests.Mocks;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Heroes;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class TransferCharacterStateTests
    {
        private readonly ConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;
        private readonly Player acknowledgedPlayer;

        public TransferCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            container.Resolve<IConnectionMessageQueue>().RegisterPeer(playerPeer);
            connectionLogic = container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));
            acknowledgedPlayer = serverComponent.BindPeerToPlayer(playerPeer);
        }

        [Fact]
        public void LoadMethod_TransitionState_LoadingState()
        {
            // Arrange
            connectionLogic.SetTransferSaveState(acknowledgedPlayer);

            // Act
            connectionLogic.Load();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetTransferSaveState(acknowledgedPlayer);

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave(acknowledgedPlayer);
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
            var heroMeetingData = new HeroMeetingData(new());
            heroMeetingData.PlayerLastMeetingTimes["Hero_Player"] = new()
            {
                ["lord_6_1"] = 1351,
            };
            var navalPlayerData = new NavalPlayerData(new(), new(), new(), new());
            navalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"] = new() { "figurehead_1" };
            navalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player"] = 2702;
            var session = new CoopSession(
                string.Empty,
                Array.Empty<Player>(),
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                heroMeetingData,
                null!,
                navalPlayerData);

            session.ClanFinance["Hero_Player"] = new ClanFinanceSettings("Clan_Coop", "Hero_Leader", 600);
            serverComponent.Container.Resolve<Mock<ICoopSessionProvider>>()
                .SetupGet(provider => provider.CoopSession)
                .Returns(session);

            // Act — entering the state packages the save and sends it to the joining peer.
            connectionLogic.TransferSave(acknowledgedPlayer);

            // Assert — exactly one save chunk for this tiny save, carrying the deflate-compressed
            // save data to the joining peer.
            var packet = Assert.Single(serverComponent.TestNetwork.GetPeerPacketsFromType<GameSaveDataChunkPacket>(playerPeer));
            Assert.Equal(data, SaveDataCompression.Decompress(packet.ChunkData));
            Assert.Equal(campaignId, packet.CampaignID);
            Assert.Equal(0, packet.ChunkIndex);
            Assert.Equal(1, packet.ChunkCount);
            Assert.Equal(1351, packet.HeroMeetingData.PlayerLastMeetingTimes["Hero_Player"]["lord_6_1"]);
            Assert.Equal(600, packet.ClanFinance["Hero_Player"].DailyPayment);
            session.ClanFinance.Clear();
            Assert.Single(packet.ClanFinance);
            Assert.Equal(new[] { "figurehead_1" }, packet.NavalPlayerData.PlayerUnlockedFigureHeads["Hero_Player"]);
            Assert.Equal(2702, packet.NavalPlayerData.PlayerLastFigureheadLootTimes["Hero_Player"]);
            serverComponent.Container.Resolve<Mock<ITimeControlInterface>>()
                .Verify(m => m.ServerSetTimeControl(It.IsAny<TimeControlEnum>()), Times.Never);

            // The directed save is not sent to any other peer.
            var otherPeerGotSave =
                serverComponent.TestNetwork.SentPackets.TryGetValue(differentPeer.Id, out var packets) &&
                packets.OfType<GameSaveDataChunkPacket>().Any();
            Assert.False(otherPeerGotSave);
        }

        [Fact]
        public void EnteringState_PeerUnbound_DisconnectsWithoutTakingTheSave()
        {
            // A game over or deletion on the game thread unbinds the peer
            serverComponent.Container.Resolve<Mock<IPlayerManager>>()
                .Setup(manager => manager.TryGetCurrentController(playerPeer, out It.Ref<string>.IsAny))
                .Returns(false);

            connectionLogic.TransferSave(acknowledgedPlayer);

            AssertDisconnectedWithoutTakingTheSave();
        }

        [Fact]
        public void EnteringState_AcknowledgedPlayerReplaced_DisconnectsWithoutTakingTheSave()
        {
            // An heir swap replaces the registration while the peer stays bound to it
            connectionLogic.TransferSave(new Player("BoundController", "Hero_Heir", "MobileParty_Bound", "Clan_Bound", "Character_Heir"));

            AssertDisconnectedWithoutTakingTheSave();
        }

        private void AssertDisconnectedWithoutTakingTheSave()
        {
            serverComponent.Container.Resolve<Mock<ISaveInterface>>().Verify(save => save.SaveCurrentGame(), Times.Never);
            Assert.Empty(serverComponent.TestNetwork.SentNetworkMessages);
            Assert.Equal(ConnectionState.ShutdownRequested, playerPeer.ConnectionState);
        }

        [Fact]
        public void EnteringState_BroadcastsSavingStateAroundSnapshot()
        {
            var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
            saveMock
                .Setup(value => value.SaveCurrentGame())
                .Callback(() =>
                {
                    var messages = serverComponent.TestNetwork
                        .GetPeerMessagesFromType<NetworkGameSaveStateChanged>(differentPeer)
                        .ToArray();
                    Assert.Single(messages);
                    Assert.True(messages[0].IsSaving);
                    Assert.Equal(1, serverComponent.TestNetwork.FlushPendingMessagesCalls);
                })
                .Returns(new SaveResults(true, new byte[] { 1, 2, 3 }, "12345"));

            connectionLogic.TransferSave(acknowledgedPlayer);

            var messages = serverComponent.TestNetwork
                .GetPeerMessagesFromType<NetworkGameSaveStateChanged>(differentPeer)
                .ToArray();
            Assert.Equal(new[] { true, false }, messages.Select(message => message.IsSaving));
        }

        [Fact]
        public void EnteringState_FlushFailureStillClearsSavingState()
        {
            serverComponent.TestNetwork.FlushPendingMessagesException =
                new InvalidOperationException("Flush failed.");

            connectionLogic.TransferSave(acknowledgedPlayer);

            var messages = serverComponent.TestNetwork
                .GetPeerMessagesFromType<NetworkGameSaveStateChanged>(differentPeer)
                .ToArray();
            Assert.Equal(new[] { true, false }, messages.Select(message => message.IsSaving));
            Assert.Single(
                serverComponent.TestNetwork.GetPeerPacketsFromType<GameSaveDataChunkPacket>(playerPeer));
        }

        [Fact]
        public void EnteringState_PreservesDeflateAndChunkFramingThroughSerialization()
        {
            int chunkSize = GameSaveDataChunkPacket.ChunkSize;
            byte[] data = new byte[(chunkSize * 2) + 17];
            new Random(42).NextBytes(data);
            var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
            saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(true, data, "12345"));

            connectionLogic.TransferSave(acknowledgedPlayer);

            var serializer = serverComponent.Container.Resolve<ICommonSerializer>();
            var packets = serverComponent.TestNetwork.GetPeerPacketsFromType<GameSaveDataChunkPacket>(playerPeer)
                .Select(packet => serializer.Deserialize<GameSaveDataChunkPacket>(serializer.Serialize(packet))).ToArray();
            byte[] compressed = packets.SelectMany(packet => packet.ChunkData ?? Array.Empty<byte>()).ToArray();
            int expectedChunkCount = Math.Max(1, (compressed.Length + chunkSize - 1) / chunkSize);

            Assert.Equal(expectedChunkCount, packets.Length);
            Assert.Equal(Enumerable.Range(0, packets.Length), packets.Select(x => x.ChunkIndex));
            Assert.All(packets, packet =>
            {
                Assert.Equal(packets[0].TransferId, packet.TransferId);
                Assert.Equal(packets.Length, packet.ChunkCount);
                Assert.Equal(compressed.Length, packet.CompressedSize);
                Assert.Equal(data.Length, packet.UncompressedSize);
            });
            Assert.All(packets.SkipLast(1), packet => Assert.Equal(chunkSize, packet.ChunkData.Length));
            Assert.Equal(data, SaveDataCompression.Decompress(compressed));
            Assert.Equal("12345", packets[0].CampaignID);
            Assert.All(packets.Skip(1), packet => Assert.Null(packet.CampaignID));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InterruptedTransfer_DiscardsPostSnapshotReplayAndStopsLoading(bool peerDisconnects)
        {
            var context = serverComponent.Container.Resolve<ConnectionContext>();
            var queue = context.ConnectionMessageQueue;
            var replay = MessagePacket.Create(new NetworkGameSaveStateChanged(false),
                serverComponent.Container.Resolve<ICommonSerializer>());
            byte[] data = new byte[GameSaveDataChunkPacket.ChunkSize * 2];
            new Random(42).NextBytes(data);
            long preSnapshotBytes = -1;
            long postSnapshotBytes = 0;
            serverComponent.Container.Resolve<Mock<ISaveInterface>>()
                .Setup(save => save.SaveCurrentGame())
                .Callback(() =>
                {
                    queue.TryHandleBroadcast(playerPeer, replay);
                    queue.TryGetCatchUpPendingBytes(playerPeer, out preSnapshotBytes);
                })
                .Returns(new SaveResults(true, data, "12345"));
            var network = new Mock<INetwork>();
            int attemptedChunks = 0;
            network.Setup(value => value.SendImmediate(playerPeer, It.IsAny<IPacket>()))
                .Callback<NetPeer, IPacket>((peer, packet) =>
                {
                    attemptedChunks++;
                    if (!peerDisconnects && attemptedChunks == 2) throw new InvalidOperationException("Send failed.");
                    queue.TryHandleBroadcast(peer, replay);
                    queue.TryGetCatchUpPendingBytes(peer, out postSnapshotBytes);
                    if (peerDisconnects) peer.Disconnect();
                });
            var logic = new Mock<IConnectionLogic>();
            logic.SetupGet(value => value.Peer).Returns(playerPeer);

            var state = new TransferSaveState(
                logic.Object, context.MessageBroker, network.Object, context.CoopSessionProvider, context.SaveInterface,
                queue, context.Coalescer, context.AttachmentIdMapper, context.ServerOptionsProvider, context.LootShipHold,
                context.PlayerManager, acknowledgedPlayer);
            state.Load();

            Assert.Equal(peerDisconnects ? 1 : 2, attemptedChunks);
            Assert.Equal(0, preSnapshotBytes);
            Assert.True(postSnapshotBytes > 0);
            Assert.False(queue.TryGetCatchUpPendingBytes(playerPeer, out _));
            Assert.Equal(0, queue.FlushBatch(playerPeer).PacketsSent);
            if (!peerDisconnects) Assert.Equal(ConnectionState.ShutdownRequested, playerPeer.ConnectionState);
            logic.Verify(value => value.SetState<LoadingState>(), Times.Never);
        }
    }
}
