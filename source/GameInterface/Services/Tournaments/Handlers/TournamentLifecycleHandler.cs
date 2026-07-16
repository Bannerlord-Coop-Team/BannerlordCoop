using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using System.Collections.Generic;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed partial class TournamentSessionHandler
{
    private bool RemoveSessionAndBroadcast(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null)
            return false;
        if (!sessionRegistry.Remove(snapshot.SessionId))
        {
            if (sessionRegistry.TryGet(snapshot.SessionId, out _))
                return false;

            RemoveSessionTracking(liveCombatSessions, acceptedHitProgression, snapshot.SessionId);
            return true;
        }

        RemoveSessionTracking(liveCombatSessions, acceptedHitProgression, snapshot.SessionId);

        var removal = new NetworkTournamentSessionRemoved(snapshot.SessionId, snapshot.TownId);
        network.SendAll(removal);
        messageBroker.Publish(this, new TournamentSessionRemoved(snapshot.SessionId, snapshot.TownId));
        return true;
    }

    internal static void RemoveSessionTracking(
        HashSet<string> liveCombatSessions,
        HashSet<string> acceptedHitProgression,
        string sessionId)
    {
        liveCombatSessions.Remove(sessionId);
        RemoveAcceptedHitProgression(acceptedHitProgression, sessionId);
    }
}
