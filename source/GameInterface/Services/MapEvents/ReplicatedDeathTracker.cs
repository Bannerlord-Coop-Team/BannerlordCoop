using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Carries the owner's death result while a peer kills its puppet through native damage, so death and kill-feed
/// code use the owner's affector, killing blow and agent state instead of re-deriving them.
/// </summary>
public interface IReplicatedDeathTracker
{
    /// <summary>Opens a replicated death scope for affectedAgent on the calling thread; disposing it restores the previous death.</summary>
    IDisposable BeginReplicatedDeath(Agent affectedAgent, Agent affectorAgent, KillingBlow killingBlow, AgentState agentState);

    /// <summary>True when affectedAgent is the agent of the replicated death open on the calling thread.</summary>
    bool IsReplicatedDeath(Agent affectedAgent);

    /// <summary>Gets the owner's affector and killing blow when affectedAgent is dying from a replicated death.</summary>
    bool TryGetReplicatedDeath(Agent affectedAgent, out Agent affectorAgent, out KillingBlow killingBlow);

    /// <summary>Gets the owner's killed or unconscious result when affectedAgent is dying from a replicated death.</summary>
    bool TryGetReplicatedDeathState(Agent affectedAgent, out AgentState agentState);
}

/// <inheritdoc cref="IReplicatedDeathTracker"/>
public class ReplicatedDeathTracker : IReplicatedDeathTracker
{
    // Per thread because the scope wraps a single synchronous Agent.RegisterBlow call on the game thread.
    private readonly ThreadLocal<ReplicatedDeath> current = new ThreadLocal<ReplicatedDeath>();

    /// <inheritdoc/>
    public IDisposable BeginReplicatedDeath(Agent affectedAgent, Agent affectorAgent, KillingBlow killingBlow, AgentState agentState)
    {
        var death = new ReplicatedDeath(affectedAgent, affectorAgent, killingBlow, agentState);
        return new ThreadLocalValueScope<ReplicatedDeath>(current, death);
    }

    /// <inheritdoc/>
    public bool IsReplicatedDeath(Agent affectedAgent) => TryGetCurrent(affectedAgent, out _);

    /// <inheritdoc/>
    public bool TryGetReplicatedDeath(Agent affectedAgent, out Agent affectorAgent, out KillingBlow killingBlow)
    {
        affectorAgent = null;
        killingBlow = default;
        if (!TryGetCurrent(affectedAgent, out var death)) return false;

        affectorAgent = death.AffectorAgent;
        killingBlow = death.KillingBlow;
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetReplicatedDeathState(Agent affectedAgent, out AgentState agentState)
    {
        agentState = AgentState.None;
        if (!TryGetCurrent(affectedAgent, out var death)) return false;

        agentState = death.AgentState;
        return true;
    }

    // Finds the replicated death open on this thread when it belongs to affectedAgent.
    private bool TryGetCurrent(Agent affectedAgent, out ReplicatedDeath death)
    {
        death = current.Value;
        return death != null && ReferenceEquals(death.AffectedAgent, affectedAgent);
    }

    /// <summary>The owner's result for one replicated puppet death.</summary>
    private sealed class ReplicatedDeath
    {
        // Captures the dying puppet and the owner's affector, killing blow and agent state.
        public ReplicatedDeath(Agent affectedAgent, Agent affectorAgent, KillingBlow killingBlow, AgentState agentState)
        {
            AffectedAgent = affectedAgent;
            AffectorAgent = affectorAgent;
            KillingBlow = killingBlow;
            AgentState = agentState;
        }

        public Agent AffectedAgent { get; }
        public Agent AffectorAgent { get; }
        public KillingBlow KillingBlow { get; }
        public AgentState AgentState { get; }
    }
}
