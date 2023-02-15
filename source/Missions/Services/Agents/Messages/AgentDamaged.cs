using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal readonly struct AgentDamaged : INetworkEvent
    {
        [ProtoMember(1)]
        public float DamageAmount { get; }
        [ProtoMember(2)]
        public Guid RecievingAgent { get; }
        [ProtoMember(3)]
        public Guid AttackingAgent { get; }

        public AgentDamaged(float damageAmount, Guid recievingAgent, Guid attackingAgent)
        {
            DamageAmount = damageAmount;
            RecievingAgent = recievingAgent;
            AttackingAgent = attackingAgent;
        }
    }
}
