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

    /// <summary>[Game thread] Reconcile the retreat flag in an owner's newly received snapshot, without fleeing side effects.</summary>
    void ApplyRetreatSnapshot(Guid agentId, bool isRunningAway, long authorityRevision);

    /// <summary>[Game thread] Drop what is still waiting to apply to an agent that left the mission.</summary>
    void ForgetRemovedAgent(Agent agent);

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
    private readonly IBattleAgentFader fader;
    private readonly HashSet<Guid> pendingRouts = new HashSet<Guid>();
    // One record per agent across every retreat source: the highest authority revision wins, then the last one received.
    private readonly Dictionary<Guid, (bool Panicked, bool IsRunningAway, long AuthorityRevision)> pendingRetreats =
        new Dictionary<Guid, (bool Panicked, bool IsRunningAway, long AuthorityRevision)>();

    public PuppetRoutApplier(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ICasualtyAttributionMap casualties,
        IBattleAgentFader fader)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.casualties = casualties;
        this.fader = fader;

        messageBroker.Subscribe<NetworkBattleAgentFleeing>(Handle_NetworkBattleAgentFleeing);
        messageBroker.Subscribe<NetworkBattleAgentRetreatState>(Handle_NetworkBattleAgentRetreatState);
        messageBroker.Subscribe<NetworkBattleAgentRouted>(Handle_NetworkBattleAgentRouted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleAgentFleeing>(Handle_NetworkBattleAgentFleeing);
        messageBroker.Unsubscribe<NetworkBattleAgentRetreatState>(Handle_NetworkBattleAgentRetreatState);
        messageBroker.Unsubscribe<NetworkBattleAgentRouted>(Handle_NetworkBattleAgentRouted);
        pendingRetreats.Clear();
        pendingRouts.Clear();
    }

    private void Handle_NetworkBattleAgentFleeing(MessagePayload<NetworkBattleAgentFleeing> payload)
    {
        GameThread.RunSafe(() => ReceiveRetreat(payload.What.AgentId, panicked: true, isRunningAway: true, payload.What.AuthorityRevision));
    }

    private void Handle_NetworkBattleAgentRetreatState(MessagePayload<NetworkBattleAgentRetreatState> payload)
    {
        GameThread.RunSafe(() => ReceiveRetreat(payload.What.AgentId, panicked: false, payload.What.IsRunningAway, payload.What.AuthorityRevision));
    }

    public void ApplyRetreatSnapshot(Guid agentId, bool isRunningAway, long authorityRevision)
    {
        ReceiveRetreat(agentId, panicked: false, isRunningAway, authorityRevision);
    }

    public void ForgetRemovedAgent(Agent agent)
    {
        if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info))
            pendingRetreats.Remove(info.AgentId);
    }

    // Merges a retreat update into the agent's record, then applies it if its puppet and authority are ready.
    private void ReceiveRetreat(Guid agentId, bool panicked, bool isRunningAway, long authorityRevision)
    {
        if (pendingRetreats.TryGetValue(agentId, out var pending))
        {
            if (authorityRevision < pending.AuthorityRevision) return;
            panicked |= pending.Panicked && pending.AuthorityRevision == authorityRevision;
        }
        pendingRetreats[agentId] = (panicked, isRunningAway, authorityRevision);

        if (!TryApplyPendingRetreat(agentId))
            Logger.Information("[BattleSync] Deferring retreat state of {AgentId} until its puppet and authority are known", agentId);
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
        foreach (var agentId in new List<Guid>(pendingRetreats.Keys))
            TryApplyPendingRetreat(agentId);

        if (pendingRouts.Count == 0) return;

        var agentIds = new List<Guid>(pendingRouts);
        foreach (var agentId in agentIds)
        {
            if (TryApplyRout(agentId))
                pendingRouts.Remove(agentId);
        }
    }

    // Applies the agent's record at the registry's revision, retires it if older, and keeps it if newer, as RemoteAgentActionProcessor does.
    private bool TryApplyPendingRetreat(Guid agentId)
    {
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agentId, out var info)) return false;
        if (Mission.Current == null) return false;

        var pending = pendingRetreats[agentId];
        if (pending.AuthorityRevision > info.AuthorityRevision) return false;

        pendingRetreats.Remove(agentId);
        if (pending.AuthorityRevision < info.AuthorityRevision) return true;

        if (pending.Panicked) ApplyFleeing(info.Agent);
        info.Agent.IsRunningAway = pending.IsRunningAway;
        // A rally ends the panic too, because the owner's next one notifies again.
        if (!pending.IsRunningAway) info.PanicNotified = false;
        return true;
    }

    public void ApplyFleeing(Agent agent)
    {
        Mission mission = Mission.Current;
        if (mission == null || agent == null || !agent.IsActive()) return;

        // Tracked apart from the flag, because a team AI retreat sets the flag and a real panic can still follow it.
        if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info))
        {
            if (info.PanicNotified) return;
            info.PanicNotified = true;
        }

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
            fader.FadeOut(agent, hideInstantly: true, hideMount);
        }

        // Deregister AFTER the despawn, inside this game-thread action — same ordering rationale as
        // PuppetDeathApplier.
        pendingRetreats.Remove(agentId);
        registry.RemoveAgent(agentId);
        casualties.Forget(agentId);
        return true;
    }
}
