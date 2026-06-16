using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract]
    public readonly struct NetworkAgentKilled : IEvent
    {
        public NetworkAgentKilled(string victimAgentId, string attackingAgent, Blow blow)
        {
            VictimAgentId = victimAgentId;
            AttackingAgentId = attackingAgent;
            Blow = blow;
        }

        [ProtoMember(1)]
        public string VictimAgentId { get; }
        [ProtoMember(2)]
        public string AttackingAgentId { get; }
        [ProtoMember(3)]
        public Blow Blow { get; }
    }
}