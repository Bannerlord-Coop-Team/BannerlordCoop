using LiteNetLib;

namespace Coop.Core.Communication.PacketHandlers
{
    public enum PacketType
    {
        Invalid,
        PacketWrapper,
        Message,
        Example,
    }

    public interface IPacket
    {
        PacketType Type { get; }
        DeliveryMethod DeliveryMethod { get; }
    }
}
