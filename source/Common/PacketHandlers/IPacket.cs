using LiteNetLib;

namespace Common.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Event,
        Example,
        Test,
        Movement,
        RequestMobilePartyMovement,
        UpdateMobilePartyMovement,
        RequestUpdatePartyBehavior,
        UpdatePartyBehavior,
    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
