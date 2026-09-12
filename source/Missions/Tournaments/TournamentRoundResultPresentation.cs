using GameInterface.Services.Tournaments.Data;
using Missions.Tournaments.Messages;
using System.Linq;

namespace Missions.Tournaments;

public static class TournamentRoundResultPresentation
{
    public static string GetText(
        TournamentSessionSnapshot snapshot,
        string controllerId,
        NetworkTournamentRoundEnded result)
    {
        TournamentContestantData contestant = snapshot?.Contestants.FirstOrDefault(candidate =>
            candidate.IsHuman &&
            !candidate.IsReplaced &&
            candidate.ControllerId == controllerId);
        TournamentMatchData match = snapshot?.Rounds
            .SelectMany(round => round.Matches)
            .FirstOrDefault(candidate => candidate.MatchId == result?.MatchId);
        bool isParticipating = contestant != null && match?.Teams.Any(team =>
            team.ParticipantSlotIds.Contains(contestant.SlotId)) == true;
        if (!isParticipating)
            return "{=UBd0dEPp}Match is over";

        bool isWinner = result.WinnerSlotIds.Contains(contestant.SlotId);
        if (!isWinner)
            return result.IsTeamQualification
                ? "{=MLyBN51z}Round is over, your team is disqualified from the tournament."
                : "{=lcVauEKV}Round is over, you are disqualified from the tournament.";

        if (result.IsLastRound)
            return result.IsTeamQualification
                ? "{=wOqOQuJl}Round is over, your team survived the final round of the tournament."
                : "{=Jn0k20c3}Round is over, you survived the final round of the tournament.";

        return result.IsTeamQualification
            ? "{=fkOYvnVG}Round is over, your team is qualified for the next stage of the tournament."
            : "{=uytwdSVH}Round is over, you are qualified for the next stage of the tournament.";
    }
}