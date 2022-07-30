using Coop.NetImpl.LiteNet;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Mission
{
    public class NetworkAgent : IPacket, IPacketHandler
    {
        public Guid NetworkId { get; private set; }

        public PacketType PacketType => throw new NotImplementedException();

        public DeliveryMethod DeliveryMethod => throw new NotImplementedException();

        public byte[] Data => throw new NotImplementedException();

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            throw new NotImplementedException();
        }

        public void Move()
        {
            throw new NotImplementedException();
        }

        public void OnDeath()
        {
            throw new NotImplementedException();
        }
    }
}
