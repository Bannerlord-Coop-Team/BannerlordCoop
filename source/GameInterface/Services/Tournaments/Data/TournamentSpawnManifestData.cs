using ProtoBuf;
using System;

namespace GameInterface.Services.Tournaments.Data;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentSpawnManifestData
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string MatchId;
    [ProtoMember(3)]
    public readonly long Revision;
    [ProtoMember(6)]
    public readonly long BracketRevision;
    [ProtoMember(4)]
    public readonly long Sequence;
    [ProtoMember(5)]
    public readonly TournamentAgentSpawnData[] Agents;

    public TournamentSpawnManifestData(
        string sessionId,
        string matchId,
        long revision,
        long bracketRevision,
        long sequence,
        TournamentAgentSpawnData[] agents)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        BracketRevision = bracketRevision;
        Sequence = sequence;
        Agents = agents ?? Array.Empty<TournamentAgentSpawnData>();
    }
}
