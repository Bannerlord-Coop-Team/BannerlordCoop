using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Services.Agents.Packets
{
    public class AgentDamageData : INetworkEvent
    {
        public AgentDamageData(Guid sourceAgent, Guid targetAgent, double damage)
        {
            SourceAgent = sourceAgent;
            TargetAgent = targetAgent;
            Damage = damage;

        }

        [ProtoMember(1)]
        public Guid SourceAgent { get; }
        [ProtoMember(2)]
        public Guid TargetAgent { get; }

        [ProtoMember(3)]
        public double Damage { get; }
    }
}
