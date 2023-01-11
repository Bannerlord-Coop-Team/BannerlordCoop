using LiteNetLib;
using Missions.Services.Network;

namespace Missions.Services.Network
{
    public interface IPacket
    {
        DeliveryMethod DeliveryMethod { get; }
        PacketType PacketType { get; }
    }
}
