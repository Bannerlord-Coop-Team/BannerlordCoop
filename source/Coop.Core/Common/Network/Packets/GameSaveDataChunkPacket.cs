using Common.PacketHandlers;
using GameInterface.Services.Alleys;
using GameInterface.Services.Caravans;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Common.Network.Packets;

/// <summary>
/// Carries one bounded chunk of the compressed campaign transfer save.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct GameSaveDataChunkPacket : IPacket
{
    public const int ChunkSize = 64 * 1024;

    public readonly PacketType PacketType => PacketType.SaveDataChunk;

    public readonly DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

    [ProtoMember(1)]
    public readonly int TransferId;

    [ProtoMember(2)]
    public readonly int ChunkIndex;

    [ProtoMember(3)]
    public readonly int ChunkCount;

    [ProtoMember(4)]
    public readonly int CompressedSize;

    [ProtoMember(5)]
    public readonly int UncompressedSize;

    [ProtoMember(6)]
    public readonly byte[] ChunkData;

    [ProtoMember(7)]
    public readonly string CampaignID;

    [ProtoMember(8)]
    public readonly CraftingPlayerData CraftingPlayerData;

    [ProtoMember(9)]
    public readonly WorkshopPlayerData WorkshopPlayerData;

    [ProtoMember(10)]
    public readonly CaravansPlayerData CaravansPlayerData;

    [ProtoMember(11)]
    public readonly AlleyPlayerData AlleyPlayerData;

    [ProtoMember(12)]
    public readonly InteractionsPlayerData InteractionsPlayerData;

    [ProtoMember(13)]
    public readonly TradePlayerData TradePlayerData;

    [ProtoMember(14)]
    public readonly AttachmentIdMap AttachmentIdMap;

    [ProtoMember(15)]
    public readonly int BattleSize;

    public GameSaveDataChunkPacket(
        int transferId,
        int chunkIndex,
        int chunkCount,
        int compressedSize,
        int uncompressedSize,
        byte[] chunkData,
        string campaignID,
        CraftingPlayerData craftingPlayerData,
        WorkshopPlayerData workshopPlayerData,
        CaravansPlayerData caravansPlayerData,
        AlleyPlayerData alleyPlayerData,
        InteractionsPlayerData interactionsPlayerData,
        TradePlayerData tradePlayerData,
        AttachmentIdMap attachmentIdMap,
        int battleSize)
    {
        TransferId = transferId;
        ChunkIndex = chunkIndex;
        ChunkCount = chunkCount;
        CompressedSize = compressedSize;
        UncompressedSize = uncompressedSize;
        ChunkData = chunkData;
        CampaignID = campaignID;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        CaravansPlayerData = caravansPlayerData;
        AlleyPlayerData = alleyPlayerData;
        InteractionsPlayerData = interactionsPlayerData;
        TradePlayerData = tradePlayerData;
        AttachmentIdMap = attachmentIdMap;
        BattleSize = battleSize;
    }
}
