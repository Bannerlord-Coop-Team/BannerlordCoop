using GameInterface.Services.Tournaments.Data;
using System.Linq;

namespace GameInterface.Services.Tournaments.Handlers;

#if DEBUG
/// <summary>Applies the ordered winners selected by the debug command.</summary>
internal interface ITournamentMatchSimulation
{
    TournamentMutationStatus TryResolveMatch(
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match,
        string[] winnerTeamIds,
        out TournamentSessionSnapshot advanced);
}

internal sealed partial class TournamentSessionHandler : ITournamentMatchSimulation
{
    TournamentMutationStatus ITournamentMatchSimulation.TryResolveMatch(
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match,
        string[] winnerTeamIds,
        out TournamentSessionSnapshot advanced)
    {
        advanced = snapshot;
        var status = sessionRegistry.TryForceSimulation(snapshot.SessionId, snapshot.Revision, out var simulating);
        if (status != TournamentMutationStatus.Applied && status != TournamentMutationStatus.NoChange)
            return status;

        if (status == TournamentMutationStatus.Applied)
            BroadcastSnapshot(simulating);

        var result = tournamentGameInterface.BuildSimulatedMatchResult(
            simulating, match, winnerTeamIds, ++simulationSequence);
        return TryAdvanceSimulatedMatch(simulating, simulating.HostControllerId, result, out advanced);
    }
}
#endif

internal sealed partial class TournamentSessionHandler
{
    private const int MaximumSimulationMatches = 64;
    private long simulationSequence;

    private TournamentSessionSnapshot SimulateCurrentMatchAndAdvance(
        TournamentSessionSnapshot snapshot,
        string submittingControllerId)
    {
        if (snapshot == null)
            return snapshot;

        Logger.Information(
            "[Tournament] Simulating match session={SessionId}, match={MatchId}, revision={Revision}, bracketRevision={BracketRevision}, bracket={Bracket}",
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            DescribeBracket(snapshot.Rounds));
        if (!sessionRegistry.TryGetSimulationTeams(
                snapshot.SessionId, snapshot.Revision, out var match, out var eligibleTeams, out var winnerTeamCount) ||
            !tournamentGameInterface.TrySimulateCurrentMatchUnbiased(
                snapshot, match, eligibleTeams, winnerTeamCount, ++simulationSequence, out var result))
        {
            Logger.Warning(
                "[Tournament] Failed to simulate match session={SessionId}, match={MatchId}, bracket={Bracket}",
                snapshot.SessionId,
                snapshot.CurrentMatchId,
                DescribeBracket(snapshot.Rounds));
            return snapshot;
        }

        var status = TryAdvanceSimulatedMatch(snapshot, submittingControllerId, result, out var advanced);
        if (status != TournamentMutationStatus.Applied)
            return snapshot;

        return advanced;
    }

    private TournamentMutationStatus TryAdvanceSimulatedMatch(
        TournamentSessionSnapshot snapshot,
        string submittingControllerId,
        TournamentMatchResultData result,
        out TournamentSessionSnapshot changed)
    {
        changed = snapshot;
        if (!tournamentGameInterface.TryAdvanceBracket(snapshot, result, out var bracket))
        {
            Logger.Warning("[Tournament] Failed to advance simulated match session={SessionId}, match={MatchId}",
                snapshot.SessionId, snapshot.CurrentMatchId);
            return TournamentMutationStatus.Rejected;
        }

        Logger.Information(
            "[Tournament] Simulation produced winners session={SessionId}, match={MatchId}, winners={Winners}, nextMatch={NextMatchId}, completed={Completed}, bracket={Bracket}",
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            string.Join(",", bracket.MatchWinnerSlotIds),
            bracket.CurrentMatchId,
            bracket.IsCompleted,
            DescribeBracket(bracket.Rounds));
        var contestantScores = TournamentStateReconciliation.ReconcileContestantScores(
            snapshot,
            bracket.ContestantScores,
            out bool scoresChanged);
        Logger.Information(
            "[Tournament] Committing simulated result session={SessionId}, match={MatchId}: candidateScores={CandidateScoreCount}, canonicalScores={CanonicalScoreCount}, corrected={ScoresChanged}",
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            bracket.ContestantScores.Count,
            contestantScores.Count,
            scoresChanged);
        var status = sessionRegistry.TryApplyMatchResult(
            result,
            submittingControllerId,
            bracket.Rounds,
            contestantScores,
            bracket.CurrentMatchId,
            bracket.WinnerSlotId,
            bracket.IsCompleted,
            out changed,
            out var disconnectedLosers);
        if (status != TournamentMutationStatus.Applied)
        {
            Logger.Warning(
                "[Tournament] Rejected simulated bracket update session={SessionId}, match={MatchId}, status={Status}",
                snapshot.SessionId,
                snapshot.CurrentMatchId,
                status);
            return status;
        }

        Logger.Information(
            "[Tournament] Applied simulated bracket update session={SessionId}, nextMatch={NextMatchId}, revision={Revision}, bracketRevision={BracketRevision}",
            changed.SessionId,
            changed.CurrentMatchId,
            changed.Revision,
            changed.BracketRevision);
        ResolveBets(snapshot, bracket.MatchWinnerSlotIds);
        BroadcastSnapshot(changed);
        NotifyConnectedDisconnectLosers(disconnectedLosers);
        if (bracket.IsCompleted)
            CompleteTournament(changed);
        return status;
    }

    private static string DescribeBracket(TournamentRoundData[] rounds)
    {
        if (rounds == null) return "(null)";

        return string.Join("; ", rounds.Select(round =>
            $"{round.RoundId}[current={round.CurrentMatchIndex},matches={round.Matches.Length}] " +
            string.Join(" | ", round.Matches.Select(match =>
                $"{match.MatchId}(state={match.State},teamSize={match.TeamSize},teams={match.Teams.Length},members={string.Join("+", match.Teams.Select(team => team.ParticipantSlotIds.Length))},winners={match.WinnerSlotIds.Length})"))));
    }

    private void SimulateRemainingTournament(TournamentSessionSnapshot snapshot, bool forceSimulation = false)
    {
        int simulatedMatches = 0;
        while (snapshot != null && !snapshot.IsCompleted && simulatedMatches < MaximumSimulationMatches)
        {
            if (snapshot.Phase == TournamentSessionPhase.AwaitingChoices ||
                (forceSimulation && snapshot.Phase == TournamentSessionPhase.LiveMatch))
            {
                var status = forceSimulation
                    ? sessionRegistry.TryForceSimulation(snapshot.SessionId, snapshot.Revision, out snapshot)
                    : sessionRegistry.TryBeginEmptySimulation(snapshot.SessionId, snapshot.Revision, out snapshot);
                if (status != TournamentMutationStatus.Applied && status != TournamentMutationStatus.NoChange)
                    return;
                if (status == TournamentMutationStatus.Applied)
                    BroadcastSnapshot(snapshot);
            }

            if (snapshot.Phase != TournamentSessionPhase.SimulatingMatch &&
                snapshot.Phase != TournamentSessionPhase.LiveMatch)
            {
                return;
            }

            string hostControllerId = snapshot.HostControllerId;
            TournamentSessionSnapshot previous = snapshot;
            snapshot = SimulateCurrentMatchAndAdvance(snapshot, hostControllerId);
            if (snapshot.Revision == previous.Revision)
                return;
            simulatedMatches++;
        }
    }
}
