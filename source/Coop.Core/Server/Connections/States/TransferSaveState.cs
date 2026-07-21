using Common;
using Common.Logging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Common.Network.Packets;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection currently receiving the game state
/// through a save transfer
/// </summary>
public class TransferSaveState : ConnectionStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<TransferSaveState>();
    private static int nextSaveTransferId;

    public TransferSaveState(
        IConnectionLogic connectionLogic,
        INetwork network,
        ICoopSessionProvider coopSessionProvider,
        ISaveInterface saveInterface,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer,
        IAttachmentIdMapper attachmentIdMapper)
        : base(connectionLogic)
    {
        GameSaveDataPacket snapshot = default;
        bool snapshotCreated = false;

        GameThread.Run(() =>
        {
            // Flush pending coalesced sends before the snapshot, while this peer is still Dropping: a deferred
            // delta would otherwise be both captured in the snapshot and replayed to this peer after BeginQueueing
            // (double-apply). Guarded so a throwing send can't strand this blocking GameThread.Run.
            try
            {
                coalescer.Flush(network);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to flush coalesced sends before the join save snapshot");
            }

            var saveResults = saveInterface.SaveCurrentGame();

            // Disconnect peer on failure — before touching Data, which a failed snapshot may leave null.
            if (!saveResults.Success)
            {
                Logger.Error("Join save snapshot failed for peer {PeerId}; disconnecting", connectionLogic.Peer.Id);
                connectionLogic.Peer.Disconnect();
                return;
            }

            // Clone the mutable session DTOs at the same boundary as the campaign save. Compression and
            // packet serialization run after the game-thread action returns, while campaign ticks resume.
            snapshot = new GameSaveDataPacket(
                saveResults.Data,
                saveResults.CampaignId,
                Clone(coopSessionProvider.CoopSession?.CraftingPlayerData),
                Clone(coopSessionProvider.CoopSession?.WorkshopPlayerData),
                Clone(coopSessionProvider.CoopSession?.CaravansPlayerData),
                Clone(coopSessionProvider.CoopSession?.AlleyPlayerData),
                Clone(coopSessionProvider.CoopSession?.InteractionsPlayerData),
                Clone(coopSessionProvider.CoopSession?.TradePlayerData),
                attachmentIdMapper.BuildServerMap());

            // Start holding this peer's broadcasts now that the snapshot has been taken. The whole save
            // runs in a blocking GameThread.Run call issued from the network thread, so the poller is
            // parked for its duration and cannot broadcast a received delta that races the snapshot;
            // taking the cut right after the snapshot cleanly separates "in the save" (dropped while
            // Dropping) from "after the save" (queued for replay).
            connectionMessageQueue.BeginQueueing(ConnectionLogic.Peer);
            snapshotCreated = true;
        }, blocking: true);

        if (!snapshotCreated) return;

        byte[] compressedSave = SaveDataCompression.Compress(snapshot.GameSaveData);
        SendSaveChunks(network, snapshot, compressedSave);
    }

    private void SendSaveChunks(INetwork network, GameSaveDataPacket snapshot, byte[] compressedSave)
    {
        int transferId = Interlocked.Increment(ref nextSaveTransferId);
        int chunkCount = Math.Max(1, (compressedSave.Length + GameSaveDataChunkPacket.ChunkSize - 1) / GameSaveDataChunkPacket.ChunkSize);

        Logger.Information(
            "Sending join save transfer {TransferId} to peer {PeerId}: {ChunkCount} chunks, {CompressedSize:N0} compressed bytes, {UncompressedSize:N0} save bytes",
            transferId,
            ConnectionLogic.Peer.Id,
            chunkCount,
            compressedSave.Length,
            snapshot.GameSaveData?.Length ?? 0);

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int offset = chunkIndex * GameSaveDataChunkPacket.ChunkSize;
            int length = Math.Min(GameSaveDataChunkPacket.ChunkSize, compressedSave.Length - offset);
            byte[] chunkData = length <= 0 ? Array.Empty<byte>() : new byte[length];
            if (length > 0)
            {
                Buffer.BlockCopy(compressedSave, offset, chunkData, 0, length);
            }

            var chunkPacket = new GameSaveDataChunkPacket(
                transferId,
                chunkIndex,
                chunkCount,
                compressedSave.Length,
                snapshot.GameSaveData?.Length ?? 0,
                chunkData,
                chunkIndex == 0 ? snapshot.CampaignID : null,
                chunkIndex == 0 ? snapshot.CraftingPlayerData : null,
                chunkIndex == 0 ? snapshot.WorkshopPlayerData : null,
                chunkIndex == 0 ? snapshot.CaravansPlayerData : null,
                chunkIndex == 0 ? snapshot.AlleyPlayerData : null,
                chunkIndex == 0 ? snapshot.InteractionsPlayerData : null,
                chunkIndex == 0 ? snapshot.TradePlayerData : null,
                chunkIndex == 0 ? snapshot.AttachmentIdMap : null);

            network.SendImmediate(ConnectionLogic.Peer, chunkPacket);
        }
    }

    private static T Clone<T>(T value) where T : class =>
        value == null ? null : Serializer.DeepClone(value);

    public override bool IsLoading => true;

    public override void Dispose()
    {
    }

    public override void CreateCharacter()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }

    public override void Load()
    {
        ConnectionLogic.SetState<LoadingState>();
    }

    public override void TransferSave()
    {
    }
}
