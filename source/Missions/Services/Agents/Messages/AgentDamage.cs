using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Internal event for Agent damage
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct AgentDamage : IEvent
    {
        [ProtoMember(1)]
        public Agent AttackerAgent { get; }
        [ProtoMember(2)]
        public Agent VictimAgent { get; }
        [ProtoMember(3)]
        public Blow Blow { get; }
        [ProtoMember(4)]
        public AttackCollisionData AttackCollisionData { get; }

        public AgentDamage(Agent attacker, Agent victim, Blow b, AttackCollisionData collisionData)
        {
            AttackerAgent = attacker;
            VictimAgent = victim;
            AttackCollisionData = collisionData;
            Blow = b;
        }
    }
}
