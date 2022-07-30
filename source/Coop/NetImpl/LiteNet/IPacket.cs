using LiteNetLib;
using LiteNetLib.Utils;
using ProtoBuf;

namespace Coop.NetImpl.LiteNet
{
    public interface IPacket
    {
        DeliveryMethod DeliveryMethod { get; }
        PacketType PacketType { get; }
        byte[] Data { get; }
    }
}