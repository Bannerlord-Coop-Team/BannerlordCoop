using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Mirrors an authoritative agent's fleeing transition, then despawns and deregisters it when the owner
/// reports that it has fully routed out. The early transition keeps vanilla battle-end logic consistent;
/// the later removal keeps the local live-agent count consistent.
/// </summary>
public interface IPuppetRoutApplier : IDisposable
{
    /// <summary>[Game thread] Apply the fleeing state carried by a spawn catch-up record.</summary>
    void ApplyFleeing(Agent agent);

    /// <summary>
    /// [Game thread] Apply fleeing and routed messages that arrived before their puppets registered.
    /// </summary>
    void DrainPendingRouts();
}

/// <inheritdoc cref="IPuppetRoutApplier"/>
public class PuppetRoutApplier : IPuppetRoutApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetRoutApplier>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ICasualtyAttributionMap casualties;
    private readonly HashSet<Guid> pendingFleeing = new HashSet<Guid>();
    private readonly HashSet<Guid> pendingRouts = new HashSet<Guid>();

    public PuppetRoutApplier(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ICasualtyAttributionMap casualties)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.casualties = casualties;

        messageBroker.Subscribe<NetworkBattleAgentFleeing>(Handle_NetworkBattleAgentFleeing);
        messageBroker.Subscribe<NetworkBattleAgentRouted>(Handle_NetworkBattleAgentRouted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleAgentFleeing>(Handle_NetworkBattleAgentFleeing);
        messageBroker.Unsubscribe<NetworkBattleAgentRouted>(Handle_NetworkBattleAgentRouted);
        pendingFleeing.Clear();
        pendingRouts.Clear();
    }

    private void Handle_NetworkBattleAgentFleeing(MessagePayload<NetworkBattleAgentFleeing> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryApplyFleeing(payload.What.AgentId))
            {
                pendingFleeing.Add(payload.What.AgentId);
                Logger.Information("[BattleSync] Deferring fleeing state of {AgentId} until its puppet registers", payload.What.AgentId);
            }
        });
    }

    private void Handle_NetworkBattleAgentRouted(MessagePayload<NetworkBattleAgentRouted> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!TryApplyRout(payload.What.AgentId))
            {
                pendingRouts.Add(payload.What.AgentId);
                Logger.Information("[DeathDiag] Deferring rout of {AgentId} until its puppet registers", payload.What.AgentId);
            }
        });
    }

    public void DrainPendingRouts()
    {
        if (pendingFleeing.Count > 0)
        {
            var fleeingAgentIds = new List<Guid>(pendingFleeing);
            foreach (var agentId in fleeingAgentIds)
            {
                if (TryApplyFleeing(agentId))
                    pendingFleeing.Remove(agentId);
            }
        }

        if (pendingRouts.Count == 0) return;

        var agentIds = new List<Guid>(pendingRouts);
        foreach (var agentId in agentIds)
        {
            if (TryApplyRout(agentId))
                pendingRouts.Remove(agentId);
        }
    }

    private bool TryApplyFleeing(Guid agentId)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(agentId, out var info)) return false;
        if (Mission.Current == null) return false;

        ApplyFleeing(info.Agent);
        return true;
    }

    public void ApplyFleeing(Agent agent)
    {
        Mission mission = Mission.Current;
        if (mission == null || agent == null || !agent.IsActive() || agent.IsRunningAway) return;

        // Remote puppets have no AI morale component, so the native callback that sets this flag never runs.
        // Notify every mission behavior after mirroring the authoritative transition.
        agent.IsRunningAway = true;
        mission.OnAgentFleeing(agent);
    }

    private bool TryApplyRout(Guid agentId)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(agentId, out var info)) return false;
        if (Mission.Current == null) return false;

        Agent agent = info.Agent;

        // IsActive() guards the native FadeOut: a puppet already removed (duplicate rout, disconnect
        // adoption, or teardown) keeps a non-null registry entry with stale Health > 0, and FadeOut's
        // GetPtr() then access-violates. Only fade the mount when it too is still active (its own
        // FadeOut AVEs on a torn-down horse); a leftover riderless horse is handled by the mount sync.
        if (agent != null && agent.IsActive() && agent.Health > 0)
        {
            bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
            agent.FadeOut(true, hideMount);
        }

        // Deregister AFTER the despawn, inside this game-thread action — same ordering rationale as
        // PuppetDeathApplier.
        registry.RemoveAgent(agentId);
        casualties.Forget(agentId);
        return true;
    }
}
