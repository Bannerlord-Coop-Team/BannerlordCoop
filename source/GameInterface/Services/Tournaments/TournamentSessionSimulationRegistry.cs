using GameInterface.Services.Tournaments.Data;

namespace GameInterface.Services.Tournaments;

public sealed partial class TournamentSessionRegistry
{
    public TournamentMutationStatus TryForceSimulation(
        string sessionId,
        long expectedRevision,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;
            if (!session.IsActive)
                return TournamentMutationStatus.InvalidPhase;
            if (session.Phase == TournamentSessionPhase.SimulatingMatch)
                return TournamentMutationStatus.NoChange;

            session.Phase = TournamentSessionPhase.SimulatingMatch;
            session.Choices.Clear();
            session.SpawnManifest = null;
            session.LastManifestSequence = 0;
            session.Revision++;
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryBeginEmptySimulation(
        string sessionId,
        long expectedRevision,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;
            if (session.Phase != TournamentSessionPhase.AwaitingChoices)
                return TournamentMutationStatus.InvalidPhase;
            if (session.Contestants.Exists(contestant => contestant.IsHuman) || session.Spectators.Count > 0)
                return TournamentMutationStatus.Rejected;

            session.Phase = TournamentSessionPhase.SimulatingMatch;
            session.Choices.Clear();
            session.Revision++;
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }
}
