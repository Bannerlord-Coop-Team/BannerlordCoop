using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Event for agent shield damage
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkShieldDamaged : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }
        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }
        [ProtoMember(3)]
        public int InflictedDamage { get; }

        public NetworkShieldDamaged(Guid agentGuid, EquipmentIndex equipmentIndex, int inflictedDamage)
        {
            AgentGuid = agentGuid;
            EquipmentIndex = equipmentIndex;
            InflictedDamage = inflictedDamage;
        }
    }
}
