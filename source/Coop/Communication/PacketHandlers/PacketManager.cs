using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Serialization;
using Coop.Communication.MessageBroker;
using Coop.Communication.PacketHandlers;
using Coop.Mod.Messages.Network;
using Coop.Serialization;
using LiteNetLib;
using LiteNetLib.Utils;
using ProtoBuf;

namespace Coop.Communication
{
    public enum NetworkDistributionType
    {
        Invalid,

    }

    public class PacketManager : IPacketManager
    {
        private readonly NetManager netManager;
        private readonly ISerializer serializer;
        private readonly IMessageBroker messageBroker;

        private static readonly Dictionary<PacketType, List<IPacketHandler>> packetHandlers = new Dictionary<PacketType, List<IPacketHandler>>();

        public static void RegisterPacketHandler(IPacketHandler handler)
        {
            if(packetHandlers.TryGetValue(handler.PacketType, out var handlers))
            {
                if (handlers.Contains(handler)) throw new InvalidOperationException($"{handler.GetType()} is already registered.");
                handlers.Add(handler);
            }
            else
            {
                packetHandlers.Add(handler.PacketType, new List<IPacketHandler> { handler });
            }
        }

        public PacketManager(NetManager netManager, ISerializer serializer, IMessageBroker messageBroker)
        {
            this.netManager = netManager;
            this.serializer = serializer;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<BroadcastPacket>(HandleBroadcast);
            messageBroker.Subscribe<ForwardPacket>(HandleForward);
            messageBroker.Subscribe<SendPacket>(HandleSend);
        }

        public void HandleBroadcast(MessagePayload<BroadcastPacket> payload)
        {
            IPacket packet = payload.What.Packet;
            SendAll(packet);
        }

        public void HandleForward(MessagePayload<ForwardPacket> payload)
        {
            NetPeer sendingClient = payload.What.SendingClient;
            IPacket packet = payload.What.Packet;
            SendAllBut(sendingClient, packet);
        }

        public void HandleSend(MessagePayload<SendPacket> payload)
        {
            NetPeer receivingClient = payload.What.ClientToSend;
            IPacket packet = payload.What.Packet;
            Send(receivingClient, packet);
        }

        private void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            foreach (NetPeer peer in netManager.ConnectedPeerList.Where(peer => peer != netPeer))
            {
                Send(peer, packet);
            }
        }

        private void SendAll(IPacket packet)
        {
            foreach(NetPeer peer in netManager.ConnectedPeerList)
            {
                Send(peer, packet);
            }
        }

        private void Send(NetPeer netPeer, IPacket packet)
        {
            PacketWrapper wrapper = new PacketWrapper(packet);

            // Put type using writer
            NetDataWriter writer = new NetDataWriter();
            writer.Put((int)wrapper.Type);

            // Serialize and put data in writer (with length is important on receive end)
            byte[] data = serializer.Serialize(packet);
            writer.PutBytesWithLength(data);

            // Send data
            netPeer.Send(writer.Data, wrapper.DeliveryMethod);
        }

        public void HandleReceive(MessagePayload<ReceivePacket> payload)
        {
            NetPeer peer = payload.What.Peer;
            NetPacketReader reader = payload.What.Writer;

            PacketWrapper wrapper = serializer.Deserialize<PacketWrapper>(reader.GetBytesWithLength());

            PacketType packetType = wrapper.Packet.Type;
            IPacket packet = wrapper.Packet;

            if(packetHandlers.TryGetValue(packetType, out var handlers))
            {
                foreach(var handler in handlers)
                {
                    Task.Factory.StartNew(() => { handler.HandlePacket(peer, packet); });
                }
            }
        }
    }

    [ProtoContract(SkipConstructor = true)]
    internal readonly struct PacketWrapper : IPacket
    {
        public PacketType Type => PacketType.PacketWrapper;
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
