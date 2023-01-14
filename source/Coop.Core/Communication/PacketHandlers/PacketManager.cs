using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using LiteNetLib;
using LiteNetLib.Utils;
using ProtoBuf;

namespace Coop.Core.Communication.PacketHandlers
{
    public class PacketManager : IPacketManager
    {
        private readonly NetManager netManager;

        private readonly Dictionary<PacketType, List<IPacketHandler>> packetHandlers = new Dictionary<PacketType, List<IPacketHandler>>();

        public PacketManager(NetManager netManager)
        {
            this.netManager = netManager;
        }

        public void RegisterPacketHandler(IPacketHandler handler)
        {
            var handlers = packetHandlers.ContainsKey(handler.PacketType) ?
                            packetHandlers[handler.PacketType] : new List<IPacketHandler>();
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
            packetHandlers[handler.PacketType] = handlers;
        }

        public void RemovePacketHandler(IPacketHandler handler)
        {
            if (!packetHandlers.ContainsKey(handler.PacketType)) return;
            var handlers = packetHandlers[handler.PacketType];
            if (handlers.Contains(handler))
                handlers.Remove(handler);
            if (handlers.Count == 0)
                packetHandlers.Remove(handler.PacketType);
        }

        public void HandleRecieve(NetPeer peer, NetPacketReader reader)
        {
            IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetBytesWithLength());
            if (packetHandlers.TryGetValue(packet.PacketType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.HandlePacket(peer, packet);
                }
            }
        }

        public void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            foreach (NetPeer peer in netManager.ConnectedPeerList.Where(peer => peer != netPeer))
            {
                Send(peer, packet);
            }
        }

        public void SendAll(IPacket packet)
        {
            foreach (NetPeer peer in netManager.ConnectedPeerList)
            {
                Send(peer, packet);
            }
        }

        public void Send(NetPeer netPeer, IPacket packet)
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
    }

    [ProtoContract(SkipConstructor = true)]
    internal readonly struct PacketWrapper : IPacket
    {
        public PacketType PacketType => PacketType.PacketWrapper;
        public DeliveryMethod DeliveryMethod { get; }

        [ProtoMember(1)]
        public IPacket Packet { get; }

        public PacketWrapper(IPacket packet)
        {
            DeliveryMethod = packet.DeliveryMethod;
            Packet = packet;
        }
    }
}
