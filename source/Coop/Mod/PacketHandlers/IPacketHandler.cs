using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.PacketHandlers
{
    public interface IPacketHandler
    {
        PacketType PacketType { get; }

        void HandlePacket(NetPeer peer, IPacket packet);
    }
}
