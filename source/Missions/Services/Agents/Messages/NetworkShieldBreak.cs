using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// External event for agent shield breaks
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkShieldBreak : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public NetworkShieldBreak(Guid agentId, EquipmentIndex equipmentIndex)
        {
            AgentGuid = agentId;
            EquipmentIndex = equipmentIndex;
        }
    }
}