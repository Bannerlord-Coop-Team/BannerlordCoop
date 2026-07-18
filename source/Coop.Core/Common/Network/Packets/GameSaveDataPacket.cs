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
/// Carries the full campaign transfer save to a joining client.
/// </summary>
/// <remarks>
/// Sent as its own packet type rather than a <see cref="MessagePacket"/> so it is structurally
/// separate from the world-change message stream. World deltas are withheld from a joining client on
/// the server side (the connection message queue) until it has loaded and entered the campaign, so the
/// save's arrival no longer drives any client-side buffering. Uses <see cref="DeliveryMethod.ReliableOrdered"/>
/// on the dedicated bulk channel (see <c>CoopNetworkBase.BulkChannel</c>), so its tens of thousands of
/// fragments neither head-of-line block the world-sync channel nor count toward the queue depth that
/// pauses campaign time; the held-back deltas make cross-channel ordering unobservable.
/// <see cref="GameSaveData"/> is deflate-compressed (see <see cref="SaveDataCompression"/>).
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public readonly struct GameSaveDataPacket : IPacket
{
    public readonly PacketType PacketType => PacketType.SaveData;

    public readonly DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

    [ProtoMember(1)]
    public readonly byte[] GameSaveData;

    [ProtoMember(2)]
    public readonly string CampaignID;

    [ProtoMember(3)]
    public readonly CraftingPlayerData CraftingPlayerData;

    [ProtoMember(4)]
    public readonly WorkshopPlayerData WorkshopPlayerData;

    [ProtoMember(5)]
    public readonly CaravansPlayerData CaravansPlayerData;

    [ProtoMember(6)]
    public readonly AlleyPlayerData AlleyPlayerData;

    [ProtoMember(7)]
    public readonly InteractionsPlayerData InteractionsPlayerData;

    [ProtoMember(8)]
    public readonly TradePlayerData TradePlayerData;

    [ProtoMember(9)]
    public readonly AttachmentIdMap AttachmentIdMap;

    public GameSaveDataPacket(
        byte[] gameSaveData,
        string campaignID,
        CraftingPlayerData craftingPlayerData,
        WorkshopPlayerData workshopPlayerData,
        CaravansPlayerData caravansPlayerData,
        AlleyPlayerData alleyPlayerData,
        InteractionsPlayerData interactionsPlayerData,
        TradePlayerData tradePlayerData,
        AttachmentIdMap attachmentIdMap)
    {
        GameSaveData = gameSaveData;
        CampaignID = campaignID;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        CaravansPlayerData = caravansPlayerData;
        AlleyPlayerData = alleyPlayerData;
        InteractionsPlayerData = interactionsPlayerData;
        TradePlayerData = tradePlayerData;
        AttachmentIdMap = attachmentIdMap;
    }
}
