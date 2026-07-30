using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Missions.Agents.Packets
{
    [ProtoContract]
    public readonly struct AgentEquipmentPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.AgentEquipment;

        [ProtoMember(1)]
        public string IdentityScopeId { get; }
        [ProtoMember(2)]
        public ushort[] AgentIds { get; }
        [ProtoMember(3)]
        public Guid[] AgentGuids { get; }
        [ProtoMember(4)]
        public AgentEquipmentData[] Equipment { get; }

        public AgentEquipmentPacket(
            string identityScopeId,
            ushort[] agentIds,
            AgentEquipmentData[] equipment)
        {
            IdentityScopeId = identityScopeId;
            AgentIds = agentIds;
            AgentGuids = null;
            Equipment = equipment;
        }

        public AgentEquipmentPacket(Guid[] agentGuids, AgentEquipmentData[] equipment)
        {
            IdentityScopeId = null;
            AgentIds = null;
            AgentGuids = agentGuids;
            Equipment = equipment;
        }
    }
}
