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
        SaveData,
        Relay,
        AgentAction,
        MountMovement,
        AggregateMessage,
        CampaignTime,
        AgentEquipment,
        CompressedMovement
    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
