using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkAgentDamage : INetworkEvent
    {

        [ProtoMember(1)]
        public Guid AttackerAgentId { get; }
        [ProtoMember(2)]
        public Guid VictimAgentId { get; }
        [ProtoMember(3)]
        public Blow Blow { get; }
        [ProtoMember(4)]
        public AttackCollisionData AttackCollisionData { get; }

        public NetworkAgentDamage(
            Guid attackerAgentId,
            Guid victimAgentId, 
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
