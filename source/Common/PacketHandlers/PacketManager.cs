using LiteNetLib;
using ProtoBuf;
using System.Collections.Generic;

namespace Common.PacketHandlers
{
    public interface IPacketManager
    {
        void HandleRecieve(NetPeer peer, IPacket packet);
        void RegisterPacketHandler(IPacketHandler handler);
        void RemovePacketHandler(IPacketHandler handler);
    }

    public class PacketManager : IPacketManager
    {
        private readonly Dictionary<PacketType, List<IPacketHandler>> packetHandlers = new Dictionary<PacketType, List<IPacketHandler>>();

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

        public void HandleRecieve(NetPeer peer, IPacket packet)
        {
            if (packetHandlers.TryGetValue(packet.PacketType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.HandlePacket(peer, packet);
                }
            }
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public readonly struct PacketWrapper : IPacket
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
