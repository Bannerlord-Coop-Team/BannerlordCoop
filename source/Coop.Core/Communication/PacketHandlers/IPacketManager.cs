using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketManager
    {
        void HandleRecieve(NetPeer peer, IPacket packet);
        void RegisterPacketHandler(IPacketHandler handler);
        void RemovePacketHandler(IPacketHandler handler);
    }
}