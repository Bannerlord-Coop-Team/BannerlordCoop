using GameInterface.Services.Tournaments.Data;
using System.Linq;

namespace Missions.Tournaments;

public static class TournamentMatchTransitionRules
{
    public static bool PreservesRunningMatch(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot updated) =>
        previous != null &&
        updated != null &&
        previous.Phase == TournamentSessionPhase.LiveMatch &&
        updated.Phase == TournamentSessionPhase.LiveMatch &&
        previous.CurrentMatchId == updated.CurrentMatchId;

    public static bool RequiresBracketRefresh(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot updated) =>
        previous != null &&
        updated != null &&
        !PreservesRunningMatch(previous, updated) &&
        (updated.BracketRevision > previous.BracketRevision ||
         updated.CurrentMatchId != previous.CurrentMatchId);

    public static bool RequiresArenaCleanup(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot updated) =>
        previous != null &&
        updated != null &&
        previous.Phase == TournamentSessionPhase.LiveMatch &&
        !PreservesRunningMatch(previous, updated);

    public static bool CompletedByDisconnect(
        TournamentSessionSnapshot previous,
        TournamentSessionSnapshot updated)
    {
        if (previous == null || updated?.IsCompleted != true ||
            (previous.Phase != TournamentSessionPhase.LiveMatch &&
             previous.Phase != TournamentSessionPhase.SimulatingMatch))
            return false;

        var final = updated.Rounds.SelectMany(round => round.Matches)
            .FirstOrDefault(match => match.MatchId == previous.CurrentMatchId);
        return final != null && updated.Contestants.Any(contestant =>
            contestant.IsDisconnected &&
            final.Teams.Any(team => team.ParticipantSlotIds.Contains(contestant.SlotId)));
    }
}
