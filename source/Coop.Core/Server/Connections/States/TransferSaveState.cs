using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Interfaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships;
using LiteNetLib;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
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
    private readonly bool transferComplete;

    public TransferSaveState(
        IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        ICoopSessionProvider coopSessionProvider,
        ISaveInterface saveInterface,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer,
        IAttachmentIdMapper attachmentIdMapper,
        IServerOptionsProvider serverOptionsProvider,
        ILootShipHold lootShipHold)
        : base(connectionLogic)
    {
        byte[] saveData = null;
        GameSaveDataPacket snapshot = default;
        var snapshotGate = new object();
        bool snapshotAbandoned = false;
        var peer = connectionLogic.Peer;

        try
        {
            GameThread.RunSafe(CaptureSnapshot, blocking: true, context: "Capture join save snapshot");
            if (saveData == null || peer.ConnectionState is ConnectionState.Disconnected or ConnectionState.ShutdownRequested)
                return;

            int uncompressedSize = saveData.Length;
            var (chunks, compressedSize) = SaveDataCompression.CompressChunks(saveData);
            saveData = null;
            transferComplete = SendSaveChunks(network, snapshot, chunks, compressedSize, uncompressedSize);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Join save transfer failed for peer {PeerId}; disconnecting", peer.Id);
        }
        finally
        {
            if (!transferComplete)
            {
                // A timed-out or canceled wait can leave the capture running on the game thread.
                lock (snapshotGate) snapshotAbandoned = true;
                connectionMessageQueue.AbortCatchUp(peer);
                peer.Disconnect();
            }
        }

        void CaptureSnapshot()
        {
            string[] heldLootShipIds = Array.Empty<string>();

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

            try
            {
                try
                {
                    messageBroker.Publish(this, new GameSaveStateChanged(true));
                    // The poll thread is blocked on this action, so flush before the save starts.
                    network.FlushPendingMessages();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to broadcast the join save start");
                }

                // A held loot ship has no owner, so neither the save nor the id map would carry it to the joining client
                heldLootShipIds = lootShipHold.AttachForJoinSnapshot();

                var saveResults = saveInterface.SaveCurrentGame();

                if (!saveResults.Success || saveResults.Data == null)
                {
                    Logger.Error("Join save snapshot failed for peer {PeerId}; disconnecting", peer.Id);
                    return;
                }

                // Clone the mutable session DTOs at the same boundary as the campaign save. Compression and
                // packet serialization run after the game-thread action returns, while campaign ticks resume.
                // Keep the raw save separate so it can be released before queuing the wire packets.
                snapshot = new GameSaveDataPacket(
                    gameSaveData: null,
                    saveResults.CampaignId,
                    Clone(coopSessionProvider.CoopSession?.CraftingPlayerData),
                    Clone(coopSessionProvider.CoopSession?.WorkshopPlayerData),
                    Clone(coopSessionProvider.CoopSession?.CaravansPlayerData),
                    Clone(coopSessionProvider.CoopSession?.AlleyPlayerData),
                    Clone(coopSessionProvider.CoopSession?.InteractionsPlayerData),
                    Clone(coopSessionProvider.CoopSession?.TradePlayerData),
                    Clone(coopSessionProvider.CoopSession?.InventoryPlayerData),
                    Clone(coopSessionProvider.CoopSession?.HeroMeetingData),
                    Clone(coopSessionProvider.CoopSession?.AgingPlayerData),
                    Clone(coopSessionProvider.CoopSession?.NavalPlayerData),
                    attachmentIdMapper.BuildServerMap(),
                    serverOptionsProvider.GetServerOptions(),
                    Clone(coopSessionProvider.CoopSession?.ClanFinance),
                    Clone(coopSessionProvider.CoopSession?.OrderOfBattlePlayerData),
                    heldLootShipIds,
                    Clone(coopSessionProvider.CoopSession?.ViewDataPlayerData));

                // Start holding this peer's broadcasts now that the snapshot has been taken. The whole save
                // runs in a blocking GameThread.Run call issued from the network thread, so the poller is
                // parked for its duration and cannot broadcast a received delta that races the snapshot;
                // taking the cut right after the snapshot cleanly separates "in the save" (dropped while
                // Dropping) from "after the save" (queued for replay).
                lock (snapshotGate)
                {
                    if (snapshotAbandoned) return;
                    connectionMessageQueue.BeginQueueing(peer);
                    saveData = saveResults.Data;
                }
            }
            finally
            {
                try
                {
                    lootShipHold.DetachFromJoinSnapshot(heldLootShipIds);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to detach the held loot ships after the join save");
                }

                try
                {
                    messageBroker.Publish(this, new GameSaveStateChanged(false));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to broadcast the join save end");
                }
            }
        }
    }

    private bool SendSaveChunks(
        INetwork network, GameSaveDataPacket snapshot, Queue<byte[]> chunks, int compressedSize, int uncompressedSize)
    {
        var peer = ConnectionLogic.Peer;
        int transferId = Interlocked.Increment(ref nextSaveTransferId);
        int chunkCount = chunks.Count;

        Logger.Information(
            "Sending join save transfer {TransferId} to peer {PeerId}: {ChunkCount} chunks, {CompressedSize:N0} compressed bytes, {UncompressedSize:N0} save bytes",
            transferId,
            peer.Id,
            chunkCount,
            compressedSize,
            uncompressedSize);

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            if (peer.ConnectionState is ConnectionState.Disconnected or ConnectionState.ShutdownRequested)
                return false;

            var chunkPacket = new GameSaveDataChunkPacket(
                transferId,
                chunkIndex,
                chunkCount,
                compressedSize,
                uncompressedSize,
                chunks.Dequeue(),
                chunkIndex == 0 ? snapshot.CampaignID : null,
                chunkIndex == 0 ? snapshot.CraftingPlayerData : null,
                chunkIndex == 0 ? snapshot.WorkshopPlayerData : null,
                chunkIndex == 0 ? snapshot.CaravansPlayerData : null,
                chunkIndex == 0 ? snapshot.AlleyPlayerData : null,
                chunkIndex == 0 ? snapshot.InteractionsPlayerData : null,
                chunkIndex == 0 ? snapshot.TradePlayerData : null,
                chunkIndex == 0 ? snapshot.InventoryPlayerData : null,
                chunkIndex == 0 ? snapshot.HeroMeetingData : null,
                chunkIndex == 0 ? snapshot.AgingPlayerData : null,
                chunkIndex == 0 ? snapshot.NavalPlayerData : null,
                chunkIndex == 0 ? snapshot.AttachmentIdMap : null,
                chunkIndex == 0 ? snapshot.ServerOptions : null,
                chunkIndex == 0 ? snapshot.ClanFinance : null,
                chunkIndex == 0 ? snapshot.OrderOfBattlePlayerData : null,
                chunkIndex == 0 ? snapshot.HeldLootShipIds : null,
                chunkIndex == 0 ? snapshot.ViewDataPlayerData : null);

            network.SendImmediate(peer, chunkPacket);
        }
        return true;
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
        if (transferComplete) ConnectionLogic.SetState<LoadingState>();
    }

    public override void TransferSave()
    {
    }
}
