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
        UpdatePartyBehavior,
        FieldAutoSync,
        PropertyAutoSync,
    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
