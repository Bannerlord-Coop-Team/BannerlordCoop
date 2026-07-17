using Common.Messaging;
using GameInterface.Services.Tournaments.Data;

namespace GameInterface.Services.Tournaments.UI;

/// <summary>
/// Carries the server-issued snapshot through Bannerlord's parameterless tournament view creation path.
/// </summary>
public sealed class TournamentMissionUIContext : IHandler
{
    private readonly object gate = new();
    private TournamentSessionSnapshot snapshot;

    public void Set(TournamentSessionSnapshot tournamentSnapshot)
    {
        lock (gate)
        {
            snapshot = tournamentSnapshot;
        }
    }

    public bool TryGet(out TournamentSessionSnapshot tournamentSnapshot)
    {
        lock (gate)
        {
            tournamentSnapshot = snapshot;
            return tournamentSnapshot != null;
        }
    }

    public void Clear(string sessionId)
    {
        lock (gate)
        {
            if (snapshot?.SessionId == sessionId)
                snapshot = null;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            snapshot = null;
        }
    }
}
