using LiteNetLib;

namespace Common.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Message,
        Example,
        Test,
        Movement,
        RequestMobilePartyMovement,
        UpdateMobilePartyMovement,
        RequestUpdatePartyBehavior,
        FieldAutoSync,
        PropertyAutoSync,
        SaveDataChunk = 12, // 11 was the unchunked save packet.
        Relay,
        AgentAction,
        MountMovement,
        AggregateMessage,
        CampaignTime,
        AgentEquipment,
        CompressedMovement,
        Voice
    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
