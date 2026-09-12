using ProtoBuf;
using System;

namespace GameInterface.Services.Tournaments.Data;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentMatchResultData
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string MatchId;
    [ProtoMember(3)]
    public readonly long Revision;
    [ProtoMember(8)]
    public readonly long BracketRevision;
    [ProtoMember(4)]
    public readonly long Sequence;
    [ProtoMember(5)]
    public readonly string[] WinnerTeamIds;
    [ProtoMember(6)]
    public readonly string[] WinnerSlotIds;
    [ProtoMember(7)]
    public readonly TournamentTeamScoreData[] TeamScores;

    public TournamentMatchResultData(
        string sessionId,
        string matchId,
        long revision,
        long bracketRevision,
        long sequence,
        string[] winnerTeamIds,
        string[] winnerSlotIds,
        TournamentTeamScoreData[] teamScores)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        BracketRevision = bracketRevision;
        Sequence = sequence;
        WinnerTeamIds = winnerTeamIds ?? Array.Empty<string>();
        WinnerSlotIds = winnerSlotIds ?? Array.Empty<string>();
        TeamScores = teamScores ?? Array.Empty<TournamentTeamScoreData>();
    }
}

