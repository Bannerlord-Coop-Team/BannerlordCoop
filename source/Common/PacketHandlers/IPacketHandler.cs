using LiteNetLib;
using System;

namespace Common.PacketHandlers
{
    public interface IPacketHandler : IDisposable
    {
        PacketType PacketType { get; }

        void HandlePacket(NetPeer peer, IPacket packet);
    }
}
