using LiteNetLib;

namespace Coop.Core.Communication.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Event,
        Example,
        Test,
    }

    public interface IPacket
    {
        PacketType PacketType { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
