using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Alleys;
using GameInterface.Services.CampaignService.Data;
using GameInterface.Services.CampaignService.Interfaces;
using GameInterface.Services.Caravans;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Inventory;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Ships;
using GameInterface.Services.Smithing;
using GameInterface.Services.ViewData;
using GameInterface.Services.Workshops;
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

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly ISaveInterface saveInterface;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    private readonly ISendCoalescer coalescer;
    private readonly IAttachmentIdMapper attachmentIdMapper;
    private readonly IServerOptionsProvider serverOptionsProvider;
    private readonly ILootShipHold lootShipHold;
    private readonly IPlayerManager playerManager;
    private readonly Player acknowledgedPlayer;

    private readonly bool saveSent;

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
        ILootShipHold lootShipHold,
        IPlayerManager playerManager,
        Player acknowledgedPlayer)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.coopSessionProvider = coopSessionProvider;
        this.saveInterface = saveInterface;
        this.connectionMessageQueue = connectionMessageQueue;
        this.coalescer = coalescer;
        this.attachmentIdMapper = attachmentIdMapper;
        this.serverOptionsProvider = serverOptionsProvider;
        this.lootShipHold = lootShipHold;
        this.playerManager = playerManager;
        this.acknowledgedPlayer = acknowledgedPlayer;

        saveSent = SendSaveOrDisconnect();
    }

    private bool SendSaveOrDisconnect()
    {
        var peer = ConnectionLogic.Peer;
        bool sent = false;
        try
        {
            JoinSaveSnapshot? snapshot = null;
            GameThread.RunSafe(() => snapshot = CaptureSnapshot(), blocking: true, context: "Capture join save snapshot");
            if (snapshot == null) return false;
            if (peer.ConnectionState != ConnectionState.Connected)
            {
                Logger.Warning("Join save transfer to peer {PeerId} stopped after snapshot because the peer is no longer connected", peer.Id);
                return false;
            }

            int uncompressedSize = snapshot.SaveData.Length;
            var (chunks, compressedSize) = SaveDataCompression.CompressChunks(
                snapshot.SaveData, GameSaveDataChunkPacket.ChunkSize);
            // Drop the raw save so the GC can reclaim it while the chunks are sent.
            snapshot.SaveData = null;
            sent = SendSaveChunks(snapshot, chunks, compressedSize, uncompressedSize);
            return sent;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Join save transfer failed for peer {PeerId}; disconnecting", peer.Id);
            return false;
        }
        finally
        {
            if (!sent)
            {
                connectionMessageQueue.AbortCatchUp(peer);
                peer.Disconnect();
            }
        }
    }

    private JoinSaveSnapshot? CaptureSnapshot()
    {
        var peer = ConnectionLogic.Peer;
        string[] heldLootShipIds = Array.Empty<string>();

        // A game over, deletion or heir swap can remove or replace the registration this peer was told it plays,
        // and a replacement keeps the peer bound, so check the exact registration here on the game thread
        var stillPlaysIt = playerManager.TryGetCurrentPlayer(peer, out var registered) &&
            ReferenceEquals(registered, acknowledgedPlayer);
        if (!stillPlaysIt)
        {
            Logger.Warning("Peer {PeerId} no longer plays the player it was told; disconnecting it instead of sending the join save", peer.Id);
            return null;
        }

        try
        {
            heldLootShipIds = BeginSnapshot();

            var saveResults = saveInterface.SaveCurrentGame();
            if (!saveResults.Success || saveResults.Data == null)
            {
                Logger.Error("Join save snapshot failed for peer {PeerId}; disconnecting", peer.Id);
                return null;
            }

            JoinSaveSnapshot snapshot = BuildSnapshot(saveResults, heldLootShipIds);

            // The poller normally waits for this cut. If its wait timed out or was canceled,
            // BeginQueueing leaves the aborted peer's replay closed.
            connectionMessageQueue.BeginQueueing(peer);
            return snapshot;
        }
        finally
        {
            EndSnapshot(heldLootShipIds);
        }
    }

    private string[] BeginSnapshot()
    {
        // Flush pending deltas while this peer is still Dropping so they only reach it through the snapshot.
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
            messageBroker.Publish(this, new GameSaveStateChanged(true));
            // The poll thread is blocked on this action, so flush before the save starts.
            network.FlushPendingMessages();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to broadcast the join save start");
        }

        // A held loot ship has no owner, so neither the save nor the id map would carry it to the joining client.
        return lootShipHold.AttachForJoinSnapshot();
    }

    private void EndSnapshot(string[] heldLootShipIds)
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

    private JoinSaveSnapshot BuildSnapshot(SaveResults saveResults, string[] heldLootShipIds)
    {
        // Clone the session data before the game thread resumes and changes it.
        var session = coopSessionProvider.CoopSession;
        return new JoinSaveSnapshot
        {
            SaveData = saveResults.Data,
            CampaignId = saveResults.CampaignId,
            CraftingPlayerData = Clone(session?.CraftingPlayerData),
            WorkshopPlayerData = Clone(session?.WorkshopPlayerData),
            CaravansPlayerData = Clone(session?.CaravansPlayerData),
            AlleyPlayerData = Clone(session?.AlleyPlayerData),
            InteractionsPlayerData = Clone(session?.InteractionsPlayerData),
            TradePlayerData = Clone(session?.TradePlayerData),
            InventoryPlayerData = Clone(session?.InventoryPlayerData),
            HeroMeetingData = Clone(session?.HeroMeetingData),
            AgingPlayerData = Clone(session?.AgingPlayerData),
            NavalPlayerData = Clone(session?.NavalPlayerData),
            AttachmentIdMap = attachmentIdMapper.BuildServerMap(),
            ServerOptions = serverOptionsProvider.GetServerOptions(),
            ClanFinance = Clone(session?.ClanFinance),
            OrderOfBattlePlayerData = Clone(session?.OrderOfBattlePlayerData),
            HeldLootShipIds = heldLootShipIds,
            ViewDataPlayerData = Clone(session?.ViewDataPlayerData),
        };
    }

    private bool SendSaveChunks(JoinSaveSnapshot snapshot, Queue<byte[]> chunks, int compressedSize, int uncompressedSize)
    {
        // The client needs chunk zero for the session data even if compression produced no bytes.
        if (chunks.Count == 0) chunks.Enqueue(Array.Empty<byte>());

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
            if (peer.ConnectionState != ConnectionState.Connected)
            {
                Logger.Warning(
                    "Join save transfer {TransferId} to peer {PeerId} stopped before chunk {ChunkIndex} because the peer is no longer connected",
                    transferId, peer.Id, chunkIndex);
                return false;
            }

            var chunkPacket = new GameSaveDataChunkPacket(
                transferId,
                chunkIndex,
                chunkCount,
                compressedSize,
                uncompressedSize,
                chunks.Dequeue(),
                chunkIndex == 0 ? snapshot.CampaignId : null,
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
        // Callers invoke Load immediately after SetState, including when the send failed and the peer is disconnecting.
        if (saveSent) ConnectionLogic.SetState<LoadingState>();
    }

    public override void TransferSave(Player acknowledgedPlayer)
    {
    }

    /// <summary>Owns the raw save and session data captured at the same point on the game thread.</summary>
    private sealed class JoinSaveSnapshot
    {
        public byte[] SaveData { get; set; }
        public string CampaignId { get; set; }
        public CraftingPlayerData CraftingPlayerData { get; set; }
        public WorkshopPlayerData WorkshopPlayerData { get; set; }
        public CaravansPlayerData CaravansPlayerData { get; set; }
        public AlleyPlayerData AlleyPlayerData { get; set; }
        public InteractionsPlayerData InteractionsPlayerData { get; set; }
        public TradePlayerData TradePlayerData { get; set; }
        public InventoryPlayerData InventoryPlayerData { get; set; }
        public HeroMeetingData HeroMeetingData { get; set; }
        public AgingPlayerData AgingPlayerData { get; set; }
        public NavalPlayerData NavalPlayerData { get; set; }
        public AttachmentIdMap AttachmentIdMap { get; set; }
        public ServerOptions ServerOptions { get; set; }
        public Dictionary<string, ClanFinanceSettings> ClanFinance { get; set; }
        public OrderOfBattlePlayerData OrderOfBattlePlayerData { get; set; }
        public string[] HeldLootShipIds { get; set; }
        public ViewDataPlayerData ViewDataPlayerData { get; set; }
    }
}
