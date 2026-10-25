using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>[Game thread] The deck puppets' interpolation diagnostics: counters, the deck error metric and, in DEBUG, a log of
/// errors too large to be lag.</summary>
internal sealed class NavalDeckDiagnostics
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalDeckDiagnostics>();

    private readonly INetworkAgentRegistry agentRegistry;
    private readonly NavalDeckErrorMetric error = new NavalDeckErrorMetric();
    // Puppets whose target moved onto a new deck since their last deck error sample.
    private readonly HashSet<Agent> placements = new HashSet<Agent>();
    private long targetsSet, transitions, ticks, evictions, teleports;
#if DEBUG
    // DEBUG: an error past this is no lag but a pose read against the wrong hull or a parked agent (R41b, 206 to 561 m).
    private const float LargeErrorMetres = 20f;
    private const float LargeErrorLogIntervalSeconds = 5f;
    private readonly Dictionary<Agent, float> loggedAt = new Dictionary<Agent, float>();
#endif

    // The registry names an agent in the DEBUG log; it can be null.
    public NavalDeckDiagnostics(INetworkAgentRegistry agentRegistry)
    {
        this.agentRegistry = agentRegistry;
    }

    /// <summary>Starts over for a new battle.</summary>
    public void Reset()
    {
        targetsSet = transitions = ticks = evictions = teleports = 0;
        error.Reset();
        Clear();
    }

    /// <summary>Drops every puppet's per-agent state.</summary>
    public void Clear()
    {
        placements.Clear();
#if DEBUG
        loggedAt.Clear();
#endif
    }

    /// <summary>Drops one puppet's per-agent state once its target is gone.</summary>
    public void Forget(Agent agent)
    {
        placements.Remove(agent);
#if DEBUG
        loggedAt.Remove(agent);
#endif
    }

    /// <summary>Counts a received deck target; one replacing a world target is a transition, one on a new deck a placement.</summary>
    public void TargetSet(Agent agent, bool replacedWorldTarget, bool onNewDeck)
    {
        targetsSet++;
        if (replacedWorldTarget) transitions++;
        if (onNewDeck) placements.Add(agent);
    }

    /// <summary>Counts a deck target replaced by a world or mounted one.</summary>
    public void LeftDeck() => transitions++;

    /// <summary>Samples the deck error of one tick; a teleport's or a new placement's sample still measures the old spot, so it
    /// only counts as skipped.</summary>
    public void Sample(Agent agent, NavalDeckTarget deck, in MatrixFrame hull, bool teleporting, float elapsed)
    {
        ticks++;
        float sample = deck.HorizontalError(hull, agent.Position);
        bool placement = placements.Remove(agent);
        error.Record(sample, placement || teleporting);
#if DEBUG
        LogLargeError(agent, deck, hull, sample, elapsed);
#endif
    }

    /// <summary>Counts a deck puppet teleported to its target.</summary>
    public void Teleported() => teleports++;

    /// <summary>Counts a deck puppet whose hull stopped resolving.</summary>
    public void Evicted() => evictions++;

    /// <summary>The counters and error metric for the naval inspect command.</summary>
    public object Inspect(int tracked) => new
    {
        tracked, deckTargetsSet = targetsSet, deckTransitions = transitions, deckTicks = ticks, deckEvictions = evictions,
        deckTeleports = teleports, lastDeckError = error.Last, maxDeckError = error.Max, deckSamplesSkipped = error.Skipped,
        measurement = "Horizontal puppet-to-target error in the current follower hull frame, sampled before each native target write; " +
            "a teleport's or a new deck placement's sample is skipped (deckSamplesSkipped), the puppet is still at its old spot then."
    };

#if DEBUG
    // DEBUG: names the agent, its deck pose and what it stands on when the error is large, at most every few seconds per agent.
    private void LogLargeError(Agent agent, NavalDeckTarget deck, in MatrixFrame hull, float sample, float elapsed)
    {
        if (sample <= LargeErrorMetres) return;
        if (loggedAt.TryGetValue(agent, out float last) && elapsed - last < LargeErrorLogIntervalSeconds) return;

        loggedAt[agent] = elapsed;
        var stepped = agent.GetSteppedEntity();
        var support = stepped.IsValid ? stepped.Root : WeakGameEntity.Invalid;
        Guid agentId = agentRegistry != null && agentRegistry.TryGetAgentInfo(agent, out var info) ? info.AgentId : Guid.Empty;
        // Agent exposes no IsPhysicsForceClosed getter; a parked agent shows as z near 500.
        Logger.Warning("[NavalDeck] Deck error {Error:0.0} m for agent {AgentId} ({Index}) on deck ship {Ship}: local {Local}, " +
            "hull origin {HullOrigin}, agent at {Position}, standing on {Support} at {SupportOrigin}",
            sample, agentId, agent.Index, deck.Ship, deck.Local, hull.origin, agent.Position,
            support.IsValid ? support.Name : "nothing", support.IsValid ? support.GlobalPosition : Vec3.Invalid);
    }
#endif
}
