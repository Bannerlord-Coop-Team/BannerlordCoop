using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Services.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentDamageData : INetworkEvent
    {
        public AgentDamageData(Guid attackerAgentId, Guid victimAgentId, double damage)
        {
            AttackerAgentId = attackerAgentId;
            VictimAgentId = victimAgentId;
            Damage = damage;

        }

        [ProtoMember(1)]
        public Guid AttackerAgentId { get; }
        [ProtoMember(2)]
        public Guid VictimAgentId { get; }

        [ProtoMember(3)]
        public double Damage { get; }
    }
}
