using LiteNetLib;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketHandler
    {
        PacketType PacketType { get; }

        void HandlePacket(NetPeer peer, IPacket packet);
    }
}
