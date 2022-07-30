using Common;
using Common.Serialization;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Mod.Mission.Network
{
    [ProtoContract(SkipConstructor = true)]
    public class EventPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableSequenced;
        public PacketType PacketType => PacketType.Event;
        public byte[] Data => m_Data;
        [ProtoMember(1)]

        private byte[] m_Data;

        public EventPacket(object payload)
        {
            m_Data = CommonSerializer.Serialize(payload, SerializationMethod.ProtoBuf);
        }
    }
}
