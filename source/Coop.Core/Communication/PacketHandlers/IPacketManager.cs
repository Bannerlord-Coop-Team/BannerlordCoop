using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Communication.PacketHandlers
{
    public interface IPacketManager
    {
        void HandleRecieve(NetPeer peer, NetPacketReader reader);
        void RegisterPacketHandler(IPacketHandler handler);
        void RemovePacketHandler(IPacketHandler handler);
        void Send(NetPeer netPeer, IPacket packet);
        void SendAll(IPacket packet);
        void SendAllBut(NetPeer netPeer, IPacket packet);
    }
}