using Common;
using Common.Logging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Common.Network.Packets;
using GameInterface.CoopSessionData;
using GameInterface.Services.CampaignService.Interfaces;
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

    private readonly INetwork network;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly ISaveInterface saveInterface;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    private readonly ISendCoalescer coalescer;
    private readonly IAttachmentIdMapper attachmentIdMapper;
    private readonly IServerOptionsProvider serverOptionsProvider;
    private int cancelled;
    private int started;

    public TransferSaveState(
        IConnectionLogic connectionLogic,
        INetwork network,
        ICoopSessionProvider coopSessionProvider,
        ISaveInterface saveInterface,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer,
        IAttachmentIdMapper attachmentIdMapper,
        IServerOptionsProvider serverOptionsProvider)
        : base(connectionLogic)
    {
        this.network = network;
        this.coopSessionProvider = coopSessionProvider;
        this.saveInterface = saveInterface;
        this.connectionMessageQueue = connectionMessageQueue;
        this.coalescer = coalescer;
        this.attachmentIdMapper = attachmentIdMapper;
        this.serverOptionsProvider = serverOptionsProvider;
    }

    /// <summary>
    /// Captures and sends the transient join snapshot after this state has become current.
    /// A timed-out queued action checks the current state before touching the campaign, so an
    /// abandoned join cannot perform a phantom serialization minutes later.
    /// </summary>
    internal bool StartTransfer()
    {
        if (Interlocked.Exchange(ref started, 1) != 0 || !IsCurrent())
            return false;

        if (connectionMessageQueue.TryUseCachedJoinSnapshot(
                ConnectionLogic.Peer, out CachedJoinSnapshot cached))
        {
            try
            {
                Logger.Information(
                    "Reusing unchanged join snapshot for peer {PeerId}: {CompressedSize:N0} compressed bytes",
                    ConnectionLogic.Peer.Id,
                    cached.CompressedData.Length);
                SendSaveChunks(
                    network,
                    cached.Metadata,
                    cached.CompressedData,
                    cached.UncompressedSize);
                return IsCurrent();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref cancelled, 1);
                Logger.Error(ex, "Failed to send cached join save to peer {PeerId}; disconnecting", ConnectionLogic.Peer.Id);
                ConnectionLogic.Peer.Disconnect();
                return false;
            }
        }

        GameSaveDataPacket snapshot = default;
        bool snapshotCreated = false;
        Exception captureError = null;
        long snapshotGeneration = 0;

        try
        {
            GameThread.Run(() =>
            {
                if (!IsCurrent()) return;

                try
                {
                    snapshotGeneration = connectionMessageQueue.CaptureFreshJoinSnapshot(
                        ConnectionLogic.Peer,
                        () =>
                        {
                            // A deferred delta could otherwise exist both in the save and in the replay tail.
                            // CaptureFreshJoinSnapshot excludes every normal world send for this exact cut.
                            coalescer.Flush(network);

                            if (!IsCurrent())
                                throw new OperationCanceledException("The joining peer left before its save cut.");

                            var saveResults = saveInterface.SaveCurrentGame();
                            if (!saveResults.Success)
                                throw new InvalidOperationException("The in-memory join save failed.");

                            if (!IsCurrent())
                                throw new OperationCanceledException("The joining peer left during its save cut.");

                            return new GameSaveDataPacket(
                                saveResults.Data,
                                saveResults.CampaignId,
                                Clone(coopSessionProvider.CoopSession?.CraftingPlayerData),
                                Clone(coopSessionProvider.CoopSession?.WorkshopPlayerData),
                                Clone(coopSessionProvider.CoopSession?.CaravansPlayerData),
                                Clone(coopSessionProvider.CoopSession?.AlleyPlayerData),
                                Clone(coopSessionProvider.CoopSession?.InteractionsPlayerData),
                                Clone(coopSessionProvider.CoopSession?.TradePlayerData),
                                Clone(coopSessionProvider.CoopSession?.InventoryPlayerData),
                                attachmentIdMapper.BuildServerMap(),
                                serverOptionsProvider.GetServerOptions());
                        },
                        out snapshot);

                    if (!IsCurrent())
                    {
                        connectionMessageQueue.InvalidateJoinSnapshot();
                        return;
                    }
                    snapshotCreated = true;
                }
                catch (Exception ex)
                {
                    captureError = ex;
                }
            }, blocking: true, label: "Join save snapshot");
        }
        catch (Exception ex) when (ex is TimeoutException || ex is OperationCanceledException)
        {
            captureError = ex;
        }

        if (!snapshotCreated || captureError != null || !IsCurrent())
        {
            Interlocked.Exchange(ref cancelled, 1);
            if (captureError != null)
                Logger.Error(captureError, "Join save snapshot failed for peer {PeerId}; disconnecting", ConnectionLogic.Peer.Id);
            ConnectionLogic.Peer.Disconnect();
            return false;
        }

        try
        {
            byte[] compressedSave = SaveDataCompression.Compress(snapshot.GameSaveData);
            if (!IsCurrent()) return false;
            connectionMessageQueue.CompleteJoinSnapshot(
                snapshotGeneration,
                snapshot,
                compressedSave);
            SendSaveChunks(
                network,
                snapshot,
                compressedSave,
                snapshot.GameSaveData?.Length ?? 0);
            return IsCurrent();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref cancelled, 1);
            Logger.Error(ex, "Failed to compress or send join save to peer {PeerId}; disconnecting", ConnectionLogic.Peer.Id);
            ConnectionLogic.Peer.Disconnect();
            return false;
        }
    }

    private bool IsCurrent() =>
        Volatile.Read(ref cancelled) == 0 &&
        ConnectionLogic.Peer.ConnectionState == LiteNetLib.ConnectionState.Connected &&
        ReferenceEquals(ConnectionLogic.State, this);

    private void SendSaveChunks(
        INetwork network,
        GameSaveDataPacket snapshot,
        byte[] compressedSave,
        int uncompressedSize)
    {
        int transferId = Interlocked.Increment(ref nextSaveTransferId);
        int chunkCount = Math.Max(1, (compressedSave.Length + GameSaveDataChunkPacket.ChunkSize - 1) / GameSaveDataChunkPacket.ChunkSize);

        Logger.Information(
            "Sending join save transfer {TransferId} to peer {PeerId}: {ChunkCount} chunks, {CompressedSize:N0} compressed bytes, {UncompressedSize:N0} save bytes",
            transferId,
            ConnectionLogic.Peer.Id,
            chunkCount,
            compressedSave.Length,
            uncompressedSize);

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
                uncompressedSize,
                chunkData,
                chunkIndex == 0 ? snapshot.CampaignID : null,
                chunkIndex == 0 ? snapshot.CraftingPlayerData : null,
                chunkIndex == 0 ? snapshot.WorkshopPlayerData : null,
                chunkIndex == 0 ? snapshot.CaravansPlayerData : null,
                chunkIndex == 0 ? snapshot.AlleyPlayerData : null,
                chunkIndex == 0 ? snapshot.InteractionsPlayerData : null,
                chunkIndex == 0 ? snapshot.TradePlayerData : null,
                chunkIndex == 0 ? snapshot.InventoryPlayerData : null,
                chunkIndex == 0 ? snapshot.AttachmentIdMap : null,
                chunkIndex == 0 ? snapshot.ServerOptions : null);

            network.SendImmediate(ConnectionLogic.Peer, chunkPacket);
        }
    }

    private static T Clone<T>(T value) where T : class =>
        value == null ? null : Serializer.DeepClone(value);

    public override bool IsLoading => true;

    public override void Dispose()
    {
        Interlocked.Exchange(ref cancelled, 1);
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
