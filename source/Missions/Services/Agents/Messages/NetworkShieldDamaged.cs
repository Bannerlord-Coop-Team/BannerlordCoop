using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Internal event for agent shield damage
    /// </summary>
    /// 

    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkShieldDamaged : INetworkEvent
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
