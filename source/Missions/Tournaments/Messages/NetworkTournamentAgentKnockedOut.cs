using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Tournaments.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkTournamentAgentKnockedOut : IEvent
{
    [ProtoMember(1)] public readonly string SessionId;
    [ProtoMember(2)] public readonly string MatchId;
    [ProtoMember(3)] public readonly long Revision;
    [ProtoMember(4)] public readonly string OriginControllerId;
    [ProtoMember(5)] public readonly long Sequence;
    [ProtoMember(6)] public readonly Guid AgentId;

    public NetworkTournamentAgentKnockedOut(
        string sessionId,
        string matchId,
        long revision,
        string originControllerId,
        long sequence,
        Guid agentId)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        OriginControllerId = originControllerId;
        Sequence = sequence;
        AgentId = agentId;
    }
}
