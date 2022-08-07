using LiteNetLib;

namespace Coop.Communication.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Event,
        Example,
    }

    public interface IPacket
    {
        PacketType Type { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
