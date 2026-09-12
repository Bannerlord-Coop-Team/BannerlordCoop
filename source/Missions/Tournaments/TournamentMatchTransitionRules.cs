using GameInterface.Services.Tournaments.Data;

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
}
