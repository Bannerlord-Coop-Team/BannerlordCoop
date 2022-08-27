using LiteNetLib;

namespace Missions.Network
{
    public interface IPacket
    {
        DeliveryMethod DeliveryMethod { get; }
        PacketType PacketType { get; }
        byte[] Data { get; }
    }
}