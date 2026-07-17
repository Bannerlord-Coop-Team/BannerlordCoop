using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Tournaments.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkTournamentRoundEnded : IEvent
{
    [ProtoMember(1)] public readonly string SessionId;
    [ProtoMember(2)] public readonly string MatchId;
    [ProtoMember(3)] public readonly long Revision;
    [ProtoMember(4)] public readonly string OriginControllerId;
    [ProtoMember(5)] public readonly string[] WinnerSlotIds;
    [ProtoMember(6)] public readonly bool IsLastRound;
    [ProtoMember(7)] public readonly bool IsTeamQualification;

    public NetworkTournamentRoundEnded(
        string sessionId,
        string matchId,
        long revision,
        string originControllerId,
        string[] winnerSlotIds,
        bool isLastRound,
        bool isTeamQualification)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        OriginControllerId = originControllerId;
        WinnerSlotIds = winnerSlotIds ?? Array.Empty<string>();
        IsLastRound = isLastRound;
        IsTeamQualification = isTeamQualification;
    }
}