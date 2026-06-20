using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// Event for agent shield damage
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkShieldDamaged : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }
        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }
        [ProtoMember(3)]
        public int InflictedDamage { get; }

        public NetworkShieldDamaged(Guid agentId, EquipmentIndex equipmentIndex, int inflictedDamage)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            InflictedDamage = inflictedDamage;
        }
    }
}
