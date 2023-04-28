using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Internal event for Agent damage
    /// </summary>
    public readonly struct AgentDamaged : IEvent
    {
        public Agent AttackerAgent { get; }
        public Agent VictimAgent { get; }
        public Blow Blow { get; }
        public AttackCollisionData AttackCollisionData { get; }

        public AgentDamaged(Agent attacker, Agent victim, Blow b, AttackCollisionData collisionData)
        {
            AttackerAgent = attacker;
            VictimAgent = victim;
            AttackCollisionData = collisionData;
            Blow = b;
        }
    }
}
