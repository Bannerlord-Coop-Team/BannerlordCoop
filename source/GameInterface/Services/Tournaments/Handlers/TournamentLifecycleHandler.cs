using Common;
using Common.Messaging;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed partial class TournamentSessionHandler
{
    private void Handle_OrderlyShutdown(MessagePayload<TournamentOrderlyShutdownRequested> payload)
    {
        if (ModInformation.IsClient)
            return;

        GameThread.RunSafe(() =>
        {
            orderlyShutdownInProgress = true;
            try
            {
                foreach (TournamentSessionSnapshot initial in sessionRegistry.GetAll())
                {
                    if (initial.Phase == TournamentSessionPhase.Preparation)
                    {
                        RemoveSessionAndBroadcast(initial);
                        continue;
                    }

                    if (initial.IsCompleted)
                    {
                        FinalizeCompletedTournament(initial);
                        continue;
                    }

                    var status = sessionRegistry.TryForceSimulation(
                        initial.SessionId,
                        initial.Revision,
                        out var forced);
                    if (status != TournamentMutationStatus.Applied && status != TournamentMutationStatus.NoChange)
                        continue;
                    if (status == TournamentMutationStatus.Applied)
                        BroadcastSnapshot(forced);
                    SimulateRemainingTournament(forced, true);
                    if (sessionRegistry.TryGet(initial.SessionId, out var completed) && completed.IsCompleted)
                        FinalizeCompletedTournament(completed);
                }
            }
            finally
            {
                orderlyShutdownInProgress = false;
            }
        }, blocking: true, context: nameof(Handle_OrderlyShutdown));
    }

    private bool RemoveSessionAndBroadcast(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null)
            return false;
        if (!sessionRegistry.Remove(snapshot.SessionId))
            return !sessionRegistry.TryGet(snapshot.SessionId, out _);

        var removal = new NetworkTournamentSessionRemoved(snapshot.SessionId, snapshot.TownId);
        network.SendAll(removal);
        messageBroker.Publish(this, new TournamentSessionRemoved(snapshot.SessionId, snapshot.TownId));
        return true;
    }
}
