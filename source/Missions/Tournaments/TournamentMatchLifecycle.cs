using Common;
using Missions.Agents;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments;

public interface ITournamentMatchLifecycle : IDisposable
{
    string MatchId { get; }
    long MatchRevision { get; }
    bool IsClearing { get; }
    bool TryBeginMatch(string matchId, long matchRevision, bool clearNativeAgents = true);
    void ClearMatch();
}

public class TournamentMatchLifecycle : ITournamentMatchLifecycle
{
    private readonly object gate = new();
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private bool disposed;

    public TournamentMatchLifecycle(
        ICoopMissionComponent coopMissionComponent,
        INetworkWorldItemRegistry worldItemRegistry)
    {
        this.coopMissionComponent = coopMissionComponent;
        this.worldItemRegistry = worldItemRegistry;
    }

    public string MatchId { get; private set; }
    public long MatchRevision { get; private set; }
    public bool IsClearing { get; private set; }

    public bool TryBeginMatch(string matchId, long matchRevision, bool clearNativeAgents = true)
    {
        if (disposed || string.IsNullOrEmpty(matchId) || matchRevision < 0) return false;

        lock (gate)
        {
            if (MatchId == matchId && matchRevision <= MatchRevision) return false;
        }

        if (clearNativeAgents)
            ClearAgents(resetMatchIdentity: false);
        lock (gate)
        {
            MatchId = matchId;
            MatchRevision = matchRevision;
        }
        return true;
    }

    public void ClearMatch()
    {
        if (!disposed) ClearAgents(resetMatchIdentity: true);
    }

    private void ClearAgents(bool resetMatchIdentity)
    {
        lock (gate)
        {
            IsClearing = true;
        }

        try
        {
            ClearNativeAgents();
        }
        finally
        {
            FinishClear(resetMatchIdentity);
        }
    }

    private void FinishClear(bool resetMatchIdentity)
    {
        lock (gate)
        {
            if (resetMatchIdentity)
            {
                MatchId = null;
                MatchRevision = 0;
            }
            IsClearing = false;
        }
    }

    private void ClearNativeAgents()
    {
        GameThread.RunSafe(() =>
        {
            Mission mission = Mission.Current;
            if (mission == null) return;
            var registry = coopMissionComponent.AgentRegistry;
            foreach (Agent agent in mission.Agents?.ToArray() ?? Array.Empty<Agent>())
            {
                if (agent == null || agent.Mission != mission || !agent.IsActive()) continue;
                if (agent.Team == Team.Invalid) continue;

                // This is a withdrawal from the finished arena set, never a campaign casualty.
                agent.FadeOut(true, true);
            }

            mission.ClearCorpses(false);
            if (mission.MainAgent != null)
                mission.MainAgent = null;
            registry.Clear();
            foreach (var pair in worldItemRegistry.GetAll())
                if (pair.Value != null && !pair.Value.IsRemoved &&
                    pair.Value.GameEntity.IsValid)
                    pair.Value.GameEntity.Remove(0);
            worldItemRegistry.Clear();
            coopMissionComponent.AgentMovementHandler.Interpolator.Clear();
            mission.ClearMissiles();
        }, blocking: true);
    }

    public void Dispose()
    {
        if (disposed) return;
        ClearAgents(resetMatchIdentity: true);
        disposed = true;
    }
}
