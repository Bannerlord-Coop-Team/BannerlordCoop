using LiteNetLib;

namespace Coop.NetImpl.LiteNet
{
    public enum PacketType : byte
    {
        Movement,
        Event,
    }
    public interface IPacketHandler
    {
        PacketType PacketType { get; }
        void HandlePacket(NetPeer peer, IPacket packet);
    }
}