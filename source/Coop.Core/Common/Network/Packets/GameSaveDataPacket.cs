using Common.PacketHandlers;
using GameInterface.Services.Smithing;
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
/// — the same channel as <see cref="MessagePacket"/> — so it stays correctly ordered relative to the
/// deltas that follow once the client is live.
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

    public GameSaveDataPacket(byte[] gameSaveData, string campaignID, CraftingPlayerData craftingPlayerData)
    {
        GameSaveData = gameSaveData;
        CampaignID = campaignID;
        CraftingPlayerData = craftingPlayerData;
    }
}
