using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.Alleys;
using GameInterface.Services.Caravans;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MapEvents.BattleSize;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using LiteNetLib;
using Serilog;
using System;

namespace Coop.Core.Client.Services.Save.PacketHandlers;

/// <summary>
/// Reassembles the chunked transfer save and republishes it as the local save-data event.
/// </summary>
internal class GameSaveDataPacketHandler : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameSaveDataPacketHandler>();

    public PacketType PacketType => PacketType.SaveDataChunk;

    private readonly IPacketManager packetManager;
    private readonly IMessageBroker messageBroker;
    private PendingTransfer currentTransfer;
    private readonly IServerBattleSizeProvider battleSizeProvider;

    public GameSaveDataPacketHandler(
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        IServerBattleSizeProvider battleSizeProvider)
    {
        this.packetManager = packetManager;
        this.messageBroker = messageBroker;
        this.battleSizeProvider = battleSizeProvider;
        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        GameSaveDataChunkPacket chunk = (GameSaveDataChunkPacket)packet;
        if (chunk.ChunkCount <= 0 || chunk.ChunkIndex < 0 || chunk.ChunkIndex >= chunk.ChunkCount)
        {
            Logger.Warning(
                "Ignoring invalid save chunk {ChunkIndex}/{ChunkCount} for transfer {TransferId}",
                chunk.ChunkIndex,
                chunk.ChunkCount,
                chunk.TransferId);
            return;
        }

        if (currentTransfer == null || currentTransfer.TransferId != chunk.TransferId)
        {
            if (chunk.ChunkIndex != 0)
            {
                Logger.Warning(
                    "Ignoring save chunk {ChunkIndex}/{ChunkCount} for unknown transfer {TransferId}",
                    chunk.ChunkIndex,
                    chunk.ChunkCount,
                    chunk.TransferId);
                return;
            }

            currentTransfer = new PendingTransfer(chunk);
            Logger.Information(
                "Receiving host save transfer {TransferId}: {ChunkCount} chunks, {CompressedSize:N0} compressed bytes, {UncompressedSize:N0} save bytes",
                chunk.TransferId,
                chunk.ChunkCount,
                chunk.CompressedSize,
                chunk.UncompressedSize);
        }

        if (currentTransfer.TryAdd(chunk) == false)
        {
            Logger.Warning(
                "Ignoring duplicate save chunk {ChunkIndex}/{ChunkCount} for transfer {TransferId}",
                chunk.ChunkIndex,
                chunk.ChunkCount,
                chunk.TransferId);
            return;
        }

        int remaining = currentTransfer.ChunksRemaining;
        messageBroker.Publish(this, new NetworkGameSaveDataProgress(remaining));
        if (remaining > 0) return;

        PendingTransfer completedTransfer = currentTransfer;
        currentTransfer = null;
        byte[] compressedSave = completedTransfer.Assemble();
        byte[] saveData = SaveDataCompression.Decompress(compressedSave);
        Logger.Information(
            "Received host save transfer {TransferId}: {CompressedSize:N0} compressed bytes decompressed to {UncompressedSize:N0} bytes",
            completedTransfer.TransferId,
            compressedSave.Length,
            saveData.Length);

        battleSizeProvider.SetBattleSize(completedTransfer.BattleSize);

        messageBroker.Publish(this, new NetworkGameSaveDataReceived(
            saveData,
            completedTransfer.CampaignID,
            completedTransfer.CraftingPlayerData,
            completedTransfer.WorkshopPlayerData,
            completedTransfer.CaravansPlayerData,
            completedTransfer.AlleyPlayerData,
            completedTransfer.InteractionsPlayerData,
            completedTransfer.TradePlayerData,
            completedTransfer.AttachmentIdMap));
    }

    private sealed class PendingTransfer
    {
        private readonly byte[][] chunks;
        private int chunksReceived;

        public PendingTransfer(GameSaveDataChunkPacket firstChunk)
        {
            TransferId = firstChunk.TransferId;
            CompressedSize = firstChunk.CompressedSize;
            chunks = new byte[firstChunk.ChunkCount][];
            CampaignID = firstChunk.CampaignID;
            CraftingPlayerData = firstChunk.CraftingPlayerData;
            WorkshopPlayerData = firstChunk.WorkshopPlayerData;
            CaravansPlayerData = firstChunk.CaravansPlayerData;
            AlleyPlayerData = firstChunk.AlleyPlayerData;
            InteractionsPlayerData = firstChunk.InteractionsPlayerData;
            TradePlayerData = firstChunk.TradePlayerData;
            AttachmentIdMap = firstChunk.AttachmentIdMap;
            BattleSize = firstChunk.BattleSize;
        }

        public int TransferId { get; }
        public int CompressedSize { get; }
        public int ChunksRemaining => chunks.Length - chunksReceived;
        public string CampaignID { get; }
        public CraftingPlayerData CraftingPlayerData { get; }
        public WorkshopPlayerData WorkshopPlayerData { get; }
        public CaravansPlayerData CaravansPlayerData { get; }
        public AlleyPlayerData AlleyPlayerData { get; }
        public InteractionsPlayerData InteractionsPlayerData { get; }
        public TradePlayerData TradePlayerData { get; }
        public AttachmentIdMap AttachmentIdMap { get; }
        public int BattleSize { get; }

        public bool TryAdd(GameSaveDataChunkPacket chunk)
        {
            if (chunks[chunk.ChunkIndex] != null) return false;
            chunks[chunk.ChunkIndex] = chunk.ChunkData ?? Array.Empty<byte>();
            chunksReceived++;
            return true;
        }

        public byte[] Assemble()
        {
            byte[] data = new byte[CompressedSize];
            int offset = 0;
            for (int i = 0; i < chunks.Length; i++)
            {
                byte[] chunk = chunks[i];
                if (chunk == null)
                {
                    throw new InvalidOperationException($"Save transfer {TransferId} is missing chunk {i}");
                }

                Buffer.BlockCopy(chunk, 0, data, offset, chunk.Length);
                offset += chunk.Length;
            }

            if (offset != CompressedSize)
            {
                throw new InvalidOperationException(
                    $"Save transfer {TransferId} assembled {offset} bytes, expected {CompressedSize}");
            }

            return data;
        }
    }
}
