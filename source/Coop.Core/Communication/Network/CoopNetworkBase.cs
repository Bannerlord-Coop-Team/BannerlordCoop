using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Linq;

namespace Coop.Core.Communication.Network
{
    public abstract class CoopNetworkBase : INetwork
    {
        public INetworkConfiguration Configuration { get; }
        public abstract int Priority { get; }

        protected CoopNetworkBase(INetworkConfiguration configuration)
        {
            Configuration = configuration;
        }

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
            NetDataWriter writer = new NetDataWriter();

            // Serialize and put data in writer (with length is important on receive end)
            byte[] data = ProtoBufSerializer.Serialize(packet);
            writer.PutBytesWithLength(data);

            // Send data
            netPeer.Send(writer.Data, packet.DeliveryMethod);
        }

        public abstract void Start();
        public abstract void Stop();
        public abstract void SendAll(IPacket packet);
        public abstract void SendAllBut(NetPeer netPeer, IPacket packet);
        public abstract void Update(TimeSpan frameTime);
    }
}
