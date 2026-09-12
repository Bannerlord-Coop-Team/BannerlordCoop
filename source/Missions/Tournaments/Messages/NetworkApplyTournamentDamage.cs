using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkApplyTournamentDamage : IEvent
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string MatchId;
    [ProtoMember(3)]
    public readonly long Revision;
    [ProtoMember(4)]
    public readonly string OriginControllerId;
    [ProtoMember(5)]
    public readonly long Sequence;
    [ProtoMember(6)]
    public readonly Guid VictimAgentId;
    [ProtoMember(7)]
    public readonly Guid AttackerAgentId;
    [ProtoMember(8)]
    public readonly Blow Blow;
    [ProtoMember(9)]
    public readonly AttackCollisionData CollisionData;

    public NetworkApplyTournamentDamage(
        string sessionId,
        string matchId,
        long revision,
        string originControllerId,
        long sequence,
        Guid victimAgentId,
        Guid attackerAgentId,
        Blow blow,
        AttackCollisionData collisionData)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        OriginControllerId = originControllerId;
        Sequence = sequence;
        VictimAgentId = victimAgentId;
        AttackerAgentId = attackerAgentId;
        Blow = blow;
        CollisionData = collisionData;
    }
}
