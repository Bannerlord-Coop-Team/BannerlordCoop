using Common.Serialization;
using Coop.Core.Communication.PacketHandlers;
using LiteNetLib.Utils;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coop.Core.Configuration;

namespace Coop.Core.Communication.Network
{
    public abstract class CoopNetworkBase : ICoopNetwork
    {
        public abstract INetworkConfiguration Configuration { get; }
        public abstract int Priority { get; }

        public virtual void SendAllBut(NetManager netManager, NetPeer netPeer, IPacket packet)
        {
            foreach (NetPeer peer in netManager.ConnectedPeerList.Where(peer => peer != netPeer))
            {
                Send(peer, packet);
            }
        }

        protected virtual void SendAll(NetManager netManager, IPacket packet)
        {
            foreach (NetPeer peer in netManager.ConnectedPeerList)
            {
                Send(peer, packet);
            }
        }

        public virtual void Send(NetPeer netPeer, IPacket packet)
        {
            PacketWrapper wrapper = new PacketWrapper(packet);

            // Put type using writer
            NetDataWriter writer = new NetDataWriter();
            writer.Put((int)wrapper.PacketType);

            // Serialize and put data in writer (with length is important on receive end)
            byte[] data = ProtoBufSerializer.Serialize(packet);
            writer.PutBytesWithLength(data);

            // Send data
            netPeer.Send(writer.Data, wrapper.DeliveryMethod);
        }

        public abstract void Start();
        public abstract void Stop();
        public abstract void SendAll(IPacket packet);
        public abstract void SendAllBut(NetPeer netPeer, IPacket packet);
        public abstract void Update(TimeSpan frameTime);
    }
}
