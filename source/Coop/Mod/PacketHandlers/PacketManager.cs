using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.PacketHandlers
{
    public class PacketManager : IPacketManager
    {
        public void Handle(NetPeer peer, NetPacketReader writer, DeliveryMethod deliveryMethod)
        {
            throw new NotImplementedException();
        }
    }
}
