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
        Hero,
    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
