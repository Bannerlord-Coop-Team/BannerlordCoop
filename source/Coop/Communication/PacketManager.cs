using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Serialization;
using Coop.Communication.PacketHandlers;
using Coop.Serialization;
using LiteNetLib;
using LiteNetLib.Utils;
using ProtoBuf;

namespace Coop.Communication
{
    public class PacketManager : IPacketManager
    {
        private readonly Dictionary<PacketType, List<IPacketHandler>> _packetHandlers = new Dictionary<PacketType, List<IPacketHandler>>();
        
        private NetManager _netManager;
        private ISerializer _serializer;

        public PacketManager(ISerializer serializer)
        {
            _serializer = serializer;
        }

        public void Initialize(NetManager netManager)
        {
            _netManager = netManager;
        }

        public bool RegisterPacketHandler(IPacketHandler handler)
        {
            if (_packetHandlers?[handler.PacketType].Contains(handler) == true) 
                return false;

            if(_packetHandlers.TryGetValue(handler.PacketType, out List<IPacketHandler> handlers))
            {
                handlers.Add(handler);
            }
            else
            {
                _packetHandlers.Add(handler.PacketType, new List<IPacketHandler> { handler });
            }

            return true;
        }

        public bool RemovePacketHandler(IPacketHandler handler)
        {
            if(_packetHandlers.TryGetValue(handler.PacketType, out List<IPacketHandler> handlers))
            {
                if (handlers.Contains(handler))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0) _packetHandlers.Remove(handler.PacketType);
                    return true;
                }
            }
            
            return false;
        }
        public void Handle(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            PacketType packetType = (PacketType)reader.GetInt();
            if (_packetHandlers.TryGetValue(packetType, out List<IPacketHandler> handlers))
            {
                PacketWrapper wrapper = ProtoSerializerHelper.Deserialize<PacketWrapper>(reader.GetBytesWithLength());
                IPacket payload = wrapper.Packet;
                foreach (IPacketHandler handler in handlers)
                {
                    // TODO May cause threading issues
                    Task.Factory.StartNew(() => { handler.HandlePacket(peer, payload); });
                }
            }
        }

        public void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            foreach (NetPeer peer in _netManager.ConnectedPeerList.Where(peer => peer != netPeer))
            {
                Send(peer, packet);
            }
        }

        public void SendAll(IPacket packet)
        {
            foreach(NetPeer peer in _netManager.ConnectedPeerList)
            {
                Send(peer, packet);
            }
        }

        public void Send(NetPeer netPeer, IPacket packet)
        {
            PacketWrapper wrapper = new PacketWrapper(packet);

            // Put type using writer
            NetDataWriter writer = new NetDataWriter();
            writer.Put((int)wrapper.Type);

            // Serialize and put data in writer (with length is important on receive end)
            byte[] data = _serializer.Serialize(packet);
            writer.PutBytesWithLength(data);

            // Send data
            netPeer.Send(writer.Data, wrapper.DeliveryMethod);
        }

        public void Dispose()
        {
            _packetHandlers.Clear();
        }
    }

    [ProtoContract(SkipConstructor = true)]
    internal readonly struct PacketWrapper : IPacket
    {
        public PacketType Type => PacketType.PacketWrapper;
        public DeliveryMethod DeliveryMethod { get; }

        [ProtoMember(2)]
        public IPacket Packet { get; }

        public PacketWrapper(IPacket packet)
        {
            DeliveryMethod = packet.DeliveryMethod;
            Packet = packet;
        }
    }
}
