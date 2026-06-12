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
/// separate from the world-change message stream: the client uses its arrival as the divider that
/// starts buffering subsequent deltas until the campaign has finished loading (see
/// <c>LoadingPacketBuffer</c>). Uses <see cref="DeliveryMethod.ReliableOrdered"/> — the same channel
/// as <see cref="MessagePacket"/> — so it stays correctly ordered relative to those deltas.
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
