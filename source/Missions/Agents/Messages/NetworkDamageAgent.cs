using Common.Messaging;
using ProtoBuf;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// External event for agent damage
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkDamageAgent : IEvent
    {

        [ProtoMember(1)]
        public string AttackerAgentId { get; }
        [ProtoMember(2)]
        public string VictimAgentId { get; }
        [ProtoMember(3)]
        public Blow Blow { get; }
        [ProtoMember(4)]
        public AttackCollisionData AttackCollisionData { get; }

        public NetworkDamageAgent(
            string attackerAgentId,
            string victimAgentId, 
            AttackCollisionData attackCollisionData, 
            Blow blow)
        {
            AttackerAgentId = attackerAgentId;
            VictimAgentId = victimAgentId;
            AttackCollisionData = attackCollisionData;
            Blow = blow;
        }
    }
}
