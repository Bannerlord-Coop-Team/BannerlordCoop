using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract]
    public readonly struct NetworkAgentKilled : IEvent
    {
        public NetworkAgentKilled(Guid victimAgentId, Guid attackingAgent, Blow blow)
        {
            VictimAgentId = victimAgentId;
            AttackingAgentId = attackingAgent;
            Blow = blow;
        }

        [ProtoMember(1)]
        public Guid VictimAgentId { get; }
        [ProtoMember(2)]
        public Guid AttackingAgentId { get; }
        [ProtoMember(3)]
        public Blow Blow { get; }
    }
}