using LiteNetLib;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketHandler<in T>
    {
        PacketType PacketType { get; }

        void HandlePacket(T packet);
    }
}
