using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions.Agents.Packets;
using Missions.Messages;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IRemoteAgentActionProcessor : IDisposable
{
    int GetOutgoingBattleHostEpoch();
    void ClearForLocalAgent(Guid agentId, Agent agent);
    void ApplyRemoteGuardStates();
    void ReassertRemoteDefendStates();
    void Receive(AgentActionPacket packet);
    void HandleBattleHostAssigned(NetworkBattleHostAssigned message);
}

public class RemoteAgentActionProcessor : IRemoteAgentActionProcessor
{
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry battleHostRegistry;
    private readonly IMissionContext missionContext;

    // All receive-side state for one agent stays together so authority changes clear it atomically.
    private readonly Dictionary<Guid, RemoteAgentActionState> _agentStates =
        new Dictionary<Guid, RemoteAgentActionState>();
    // These indexes keep the per-tick paths scoped to agents that currently need work.
    private readonly HashSet<Guid> _pendingActionAgentIds = new HashSet<Guid>();
    private readonly HashSet<Guid> _retainedGuardAgentIds = new HashSet<Guid>();
    private readonly Dictionary<int, MigrationLineage> _migrationLineages =
        new Dictionary<int, MigrationLineage>();
    private readonly HashSet<string> _knownBattleHostControllers =
        new HashSet<string>();
    private readonly object _knownBattleHostControllersGate = new object();

    private int _appliedMigrationEpoch;
    private int _highestReceivedHostActionEpoch;
    private bool _disposed;

    private sealed class RemoteAgentActionState
    {
        public RemoteGuardState? RetainedGuard;
        public RemoteActionSequence? LastSequence;
        public Dictionary<string, RemoteAction> PendingByController;
        public MigratedActionAuthority? MigratedAuthority;

        public bool IsEmpty =>
            !RetainedGuard.HasValue
            && !LastSequence.HasValue
            && (PendingByController == null || PendingByController.Count == 0)
            && !MigratedAuthority.HasValue;
    }

    private enum RemoteActionApplyResult
    {
        Applied,
        AgentNotReady,
        Stale,
        WrongAuthority
    }

    private readonly struct RemoteGuardState
    {
        public readonly string ControllerId;
        public readonly Agent.MovementControlFlag DefendFlags;
        public readonly Agent.GuardMode GuardMode;
        public readonly int BattleHostEpoch;

        public RemoteGuardState(
            string controllerId,
            Agent.MovementControlFlag defendFlags,
            Agent.GuardMode guardMode,
            int battleHostEpoch)
        {
            ControllerId = controllerId;
            DefendFlags = defendFlags;
            GuardMode = guardMode;
            BattleHostEpoch = battleHostEpoch;
        }
    }

    private readonly struct RemoteAction
    {
        public readonly string ControllerId;
        public readonly AgentActionData Data;
        public readonly long Sequence;
        public readonly int BattleHostEpoch;

        public RemoteAction(
            string controllerId,
            AgentActionData data,
            long sequence,
            int battleHostEpoch)
        {
            ControllerId = controllerId;
            Data = data;
            Sequence = sequence;
            BattleHostEpoch = battleHostEpoch;
        }
    }

    private readonly struct MigratedActionAuthority
    {
        public readonly string ObservedAuthority;
        public readonly string ControllerId;
        public readonly int BattleHostEpoch;

        public MigratedActionAuthority(
            string observedAuthority,
            string controllerId,
            int battleHostEpoch)
        {
            ObservedAuthority = observedAuthority;
            ControllerId = controllerId;
            BattleHostEpoch = battleHostEpoch;
        }
    }

    private readonly struct MigrationLineage
    {
        public readonly string HostControllerId;
        public readonly HashSet<string> SourceAuthorities;

        public MigrationLineage(
            string hostControllerId,
            HashSet<string> sourceAuthorities)
        {
            HostControllerId = hostControllerId;
            SourceAuthorities = sourceAuthorities;
        }
    }

    private readonly struct RemoteActionSequence
    {
        public readonly string ControllerId;
        public readonly long Sequence;
        public readonly int BattleHostEpoch;

        public RemoteActionSequence(
            string controllerId,
            long sequence,
            int battleHostEpoch)
        {
            ControllerId = controllerId;
            Sequence = sequence;
            BattleHostEpoch = battleHostEpoch;
        }
    }

    public RemoteAgentActionProcessor(
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IBattleHostRegistry battleHostRegistry,
        IMissionContext missionContext)
    {
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.battleHostRegistry = battleHostRegistry;
        this.missionContext = missionContext;
    }

    public int GetOutgoingBattleHostEpoch()
    {
        string mapEventId = BattleSpawnGate.ActiveMapEventId;
        if (mapEventId == null
            || !battleHostRegistry.TryGet(mapEventId, out var assignment)
            || assignment.HostControllerId != controllerIdProvider.ControllerId)
        {
            return 0;
        }

        return assignment.Epoch;
    }

    public void ClearForLocalAgent(Guid agentId, Agent agent)
    {
        if (!_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
            return;

        _agentStates.Remove(agentId);
        _pendingActionAgentIds.Remove(agentId);
        _retainedGuardAgentIds.Remove(agentId);
        if (!state.RetainedGuard.HasValue) return;
        if (agent == null || agent.Mission != Mission.Current || !agent.IsActive()) return;

        using (new AllowedThread())
        {
            ClearRemoteDefendState(agent);
        }
    }

    public void ApplyRemoteGuardStates()
    {
        if (_disposed || Mission.Current == null) return;

        ApplyPendingRemoteActions();
        ReassertRemoteDefendStates();
    }

    public void ReassertRemoteDefendStates()
    {
        if (_disposed || Mission.Current == null) return;
        if (_retainedGuardAgentIds.Count == 0) return;

        List<Guid> staleIds = null;
        using (new AllowedThread())
        {
            foreach (Guid agentId in _retainedGuardAgentIds)
            {
                if (!_agentStates.TryGetValue(
                    agentId,
                    out RemoteAgentActionState state)
                    || !state.RetainedGuard.HasValue)
                {
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                RemoteGuardState guardState =
                    state.RetainedGuard.Value;
                if (agentRegistry.IsLocallyControlled(agentId))
                    continue;

                if (!agentRegistry.TryGetAgentInfo(agentId, out var info))
                {
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                Agent agent = info.Agent;
                if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
                {
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (!IsCurrentActionAuthority(
                    info,
                    guardState.ControllerId,
                    guardState.BattleHostEpoch))
                {
                    ClearRemoteDefendState(agent);
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                AgentActionData.ApplyDefendMovementFlags(
                    agent,
                    guardState.DefendFlags);
                Agent.GuardMode guardMode = guardState.GuardMode;
                // A Controller.None mounted rider does not realize retained defend flags without a direct guard.
                if (agent.HasMount && !AgentActionData.IsGuardMode(guardMode))
                {
                    Agent.GuardMode flagsGuardMode =
                        AgentActionData.GetGuardModeFromDefendFlags(
                            guardState.DefendFlags);
                    if (AgentActionData.IsGuardMode(flagsGuardMode))
                        guardMode = flagsGuardMode;
                }

                if (AgentActionData.IsGuardMode(guardMode))
                {
                    AgentActionData.ApplyGuardState(
                        agent,
                        guardMode);
                }
            }
        }

        if (staleIds == null) return;
        foreach (Guid agentId in staleIds)
        {
            _retainedGuardAgentIds.Remove(agentId);
            if (!_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
                continue;

            state.RetainedGuard = null;
            RemoveAgentStateIfEmpty(agentId, state);
        }
    }

    public void Receive(AgentActionPacket packet)
    {
        if (packet.AgentIds == null
            || packet.Actions == null
            || packet.Sequences == null
            || packet.AgentIds.Length != packet.Actions.Length
            || packet.AgentIds.Length != packet.Sequences.Length
            || string.IsNullOrEmpty(packet.ControllerId))
        {
            return;
        }

        ObserveHostActionEpoch(packet.BattleHostEpoch);

        // Resolve and apply the whole batch in one game-thread action, matching AgentMovementHandler.
        // Resolving here keeps this ordered behind earlier game-thread spawn/register work.
        GameThread.RunSafe(() =>
        {
            if (_disposed || Mission.Current == null) return;

            bool shouldBufferForHostAssignment =
                ShouldBufferForHostAssignment(packet);
            using (new AllowedThread())
            {
                for (int i = 0; i < packet.AgentIds.Length; i++)
                {
                    Guid agentId = packet.AgentIds[i];
                    long sequence = packet.Sequences[i];
                    if (sequence <= 0)
                        continue;

                    var action = new RemoteAction(
                        packet.ControllerId,
                        packet.Actions[i],
                        sequence,
                        packet.BattleHostEpoch);

                    if (agentRegistry.IsLocallyControlled(agentId))
                    {
                        RemoveAllPendingRemoteActions(agentId);
                        continue;
                    }

                    if (!agentRegistry.TryGetAgentInfo(agentId, out var info))
                    {
                        BufferPendingRemoteAction(agentId, action);
                        continue;
                    }

                    if (HasPendingRemoteActionAtOrAfter(agentId, action))
                        continue;

                    RemoteActionApplyResult result = TryApplyRemoteAction(
                        agentId,
                        info,
                        action,
                        removePendingBeforeApply: true);
                    if (result == RemoteActionApplyResult.AgentNotReady
                        || (result == RemoteActionApplyResult.WrongAuthority
                            && shouldBufferForHostAssignment))
                    {
                        BufferPendingRemoteAction(agentId, action);
                    }
                }
            }
        });
    }

    public void HandleBattleHostAssigned(NetworkBattleHostAssigned message)
    {
        if (_disposed) return;
        if (message.MapEventId != BattleSpawnGate.ActiveMapEventId)
            return;
        if (!battleHostRegistry.TryGet(message.MapEventId, out var assignment)
            || assignment.Epoch != message.Epoch
            || assignment.HostControllerId != message.HostControllerId)
        {
            return;
        }

        var presentControllers = new HashSet<string>(
            missionContext.ControllersInMission)
        {
            controllerIdProvider.ControllerId,
            message.HostControllerId
        };

        var candidateAuthorities = new HashSet<string>(
            agentRegistry.GetControllerIds());
        lock (_knownBattleHostControllersGate)
        {
            foreach (string controllerId in _knownBattleHostControllers)
                candidateAuthorities.Add(controllerId);
            _knownBattleHostControllers.Add(message.HostControllerId);
        }

        var absentAuthorities = new List<string>();
        foreach (string controllerId in candidateAuthorities)
        {
            if (string.IsNullOrEmpty(controllerId)
                || presentControllers.Contains(controllerId))
            {
                continue;
            }

            absentAuthorities.Add(controllerId);
        }

        string mapEventId = message.MapEventId;
        string hostControllerId = message.HostControllerId;
        int hostEpoch = message.Epoch;
        GameThread.RunSafe(() =>
        {
            if (_disposed
                || Mission.Current == null
                || BattleSpawnGate.ActiveMapEventId != mapEventId)
            {
                return;
            }

            var directSourceAuthorities = new HashSet<string>(absentAuthorities);
            var lineageSourceAuthorities =
                new HashSet<string>(directSourceAuthorities);
            foreach (var existingLineageByEpoch in _migrationLineages)
            {
                MigrationLineage existingLineage =
                    existingLineageByEpoch.Value;
                bool sameGeneration =
                    existingLineageByEpoch.Key == hostEpoch
                    && existingLineage.HostControllerId == hostControllerId;
                if (sameGeneration
                    || directSourceAuthorities.Contains(
                        existingLineage.HostControllerId))
                {
                    lineageSourceAuthorities.UnionWith(
                        existingLineage.SourceAuthorities);
                }
            }

            _migrationLineages[hostEpoch] =
                new MigrationLineage(
                    hostControllerId,
                    lineageSourceAuthorities);

            List<Guid> inheritedAgentIds = null;
            foreach (var stateByAgent in _agentStates)
            {
                if (!stateByAgent.Value.MigratedAuthority.HasValue)
                    continue;

                MigratedActionAuthority migrated =
                    stateByAgent.Value.MigratedAuthority.Value;
                if (migrated.BattleHostEpoch <= hostEpoch
                    && directSourceAuthorities.Contains(
                        migrated.ControllerId))
                {
                    (inheritedAgentIds ??= new List<Guid>())
                        .Add(stateByAgent.Key);
                }
            }

            foreach (string observedAuthority in directSourceAuthorities)
            {
                foreach (CoopAgentInfo info in agentRegistry.GetAgents(
                    observedAuthority))
                {
                    RemoteAgentActionState state =
                        GetOrCreateAgentState(info.AgentId);
                    if (state.MigratedAuthority.HasValue
                        && state.MigratedAuthority.Value.BattleHostEpoch > hostEpoch)
                    {
                        continue;
                    }

                    state.MigratedAuthority =
                        new MigratedActionAuthority(
                            observedAuthority,
                            hostControllerId,
                            hostEpoch);
                }
            }

            if (inheritedAgentIds != null)
            {
                foreach (Guid agentId in inheritedAgentIds)
                {
                    RemoteAgentActionState state = _agentStates[agentId];
                    MigratedActionAuthority inherited =
                        state.MigratedAuthority.Value;
                    state.MigratedAuthority =
                        new MigratedActionAuthority(
                            inherited.ObservedAuthority,
                            hostControllerId,
                            hostEpoch);
                }
            }

            if (_appliedMigrationEpoch < hostEpoch)
                _appliedMigrationEpoch = hostEpoch;
        });
    }

    private void ApplyPendingRemoteActions()
    {
        if (_pendingActionAgentIds.Count == 0) return;

        List<Guid> resolvedIds = null;
        int appliedMigrationEpoch = _appliedMigrationEpoch;
        using (new AllowedThread())
        {
            foreach (Guid agentId in _pendingActionAgentIds)
            {
                if (!_agentStates.TryGetValue(
                    agentId,
                    out RemoteAgentActionState state))
                {
                    (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                Dictionary<string, RemoteAction> pendingByController =
                    state.PendingByController;
                if (pendingByController == null
                    || pendingByController.Count == 0)
                {
                    (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (agentRegistry.IsLocallyControlled(agentId))
                {
                    (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (!agentRegistry.TryGetAgentInfo(agentId, out var info))
                    continue;

                PromotePendingMigration(
                    info,
                    state,
                    pendingByController);
                string authority = GetCurrentActionAuthority(
                    info,
                    out int requiredHostEpoch);
                RemoveExpiredPendingActions(
                    pendingByController,
                    authority,
                    requiredHostEpoch,
                    appliedMigrationEpoch);

                bool hasCurrentPending = pendingByController.TryGetValue(
                    authority,
                    out RemoteAction pending);
                if (!hasCurrentPending)
                {
                    if (pendingByController.Count == 0)
                        (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                RemoteActionApplyResult result = TryApplyRemoteAction(
                    agentId,
                    info,
                    pending,
                    removePendingBeforeApply: false);
                if (result == RemoteActionApplyResult.AgentNotReady)
                    continue;
                if (result == RemoteActionApplyResult.WrongAuthority
                    && pending.BattleHostEpoch > appliedMigrationEpoch)
                {
                    continue;
                }

                pendingByController.Remove(pending.ControllerId);
                if (pendingByController.Count == 0)
                    (resolvedIds ??= new List<Guid>()).Add(agentId);
            }
        }

        if (resolvedIds == null) return;
        foreach (Guid agentId in resolvedIds)
        {
            _pendingActionAgentIds.Remove(agentId);
            if (!_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
                continue;

            state.PendingByController = null;
            RemoveAgentStateIfEmpty(agentId, state);
        }
    }

    private RemoteActionApplyResult TryApplyRemoteAction(
        Guid agentId,
        CoopAgentInfo info,
        RemoteAction action,
        bool removePendingBeforeApply)
    {
        if (!IsCurrentActionAuthority(
            info,
            action.ControllerId,
            action.BattleHostEpoch))
        {
            return RemoteActionApplyResult.WrongAuthority;
        }

        if (IsStaleRemoteAction(agentId, action))
        {
            return RemoteActionApplyResult.Stale;
        }

        Agent agent = info.Agent;
        if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
            return RemoteActionApplyResult.AgentNotReady;

        if (removePendingBeforeApply)
            RemovePendingRemoteAction(agentId, action);

        action.Data.Apply(agent);
        RecordRemoteActionSequence(agentId, action);
        UpdateRemoteGuardState(agentId, action, agent);
        return RemoteActionApplyResult.Applied;
    }

    private void PromotePendingMigration(
        CoopAgentInfo info,
        RemoteAgentActionState state,
        Dictionary<string, RemoteAction> actionsByController)
    {
        int highestReceivedEpoch = Volatile.Read(
            ref _highestReceivedHostActionEpoch);
        foreach (RemoteAction pending in actionsByController.Values)
        {
            if (pending.BattleHostEpoch <= 0
                || pending.BattleHostEpoch < highestReceivedEpoch
                || !_migrationLineages.TryGetValue(
                    pending.BattleHostEpoch,
                    out MigrationLineage lineage)
                || lineage.HostControllerId != pending.ControllerId
                || !lineage.SourceAuthorities.Contains(info.CurrentAuthority)
                || !IsCurrentBattleHostGeneration(
                    pending.ControllerId,
                    pending.BattleHostEpoch))
            {
                continue;
            }

            if (state.MigratedAuthority.HasValue
                && state.MigratedAuthority.Value.BattleHostEpoch
                    > pending.BattleHostEpoch)
            {
                continue;
            }

            state.MigratedAuthority =
                new MigratedActionAuthority(
                    info.CurrentAuthority,
                    pending.ControllerId,
                    pending.BattleHostEpoch);
        }
    }

    private void UpdateRemoteGuardState(
        Guid agentId,
        RemoteAction action,
        Agent agent)
    {
        Agent.MovementControlFlag defendFlags = action.Data.DefendFlags;
        Agent.GuardMode guardMode = action.Data.GuardMode;
        if (defendFlags != Agent.MovementControlFlag.None
            || AgentActionData.IsGuardMode(guardMode))
        {
            GetOrCreateAgentState(agentId).RetainedGuard =
                new RemoteGuardState(
                    action.ControllerId,
                    defendFlags,
                    guardMode,
                    action.BattleHostEpoch);
            _retainedGuardAgentIds.Add(agentId);
            return;
        }

        _retainedGuardAgentIds.Remove(agentId);
        if (_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
        {
            state.RetainedGuard = null;
            RemoveAgentStateIfEmpty(agentId, state);
        }
        ClearRemoteDefendState(agent);
    }

    private static void ClearRemoteDefendState(Agent agent)
    {
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            Agent.MovementControlFlag.None);
        AgentActionData.ApplyGuardState(agent, Agent.GuardMode.None);
    }

    private void BufferPendingRemoteAction(Guid agentId, RemoteAction action)
    {
        if (action.BattleHostEpoch > 0
            && action.BattleHostEpoch < Volatile.Read(
                ref _highestReceivedHostActionEpoch))
        {
            return;
        }

        RemoteAgentActionState state = GetOrCreateAgentState(agentId);
        Dictionary<string, RemoteAction> actionsByController =
            state.PendingByController;
        if (actionsByController == null)
        {
            actionsByController = new Dictionary<string, RemoteAction>();
            state.PendingByController = actionsByController;
            _pendingActionAgentIds.Add(agentId);
        }

        if (actionsByController.TryGetValue(action.ControllerId, out var existing))
        {
            if (existing.BattleHostEpoch > action.BattleHostEpoch)
                return;
            if (existing.BattleHostEpoch == action.BattleHostEpoch
                && existing.Sequence >= action.Sequence)
                return;
        }

        actionsByController[action.ControllerId] = action;
    }

    private bool IsStaleRemoteAction(Guid agentId, RemoteAction action)
    {
        return _agentStates.TryGetValue(agentId, out RemoteAgentActionState state)
            && state.LastSequence.HasValue
            && state.LastSequence.Value.ControllerId == action.ControllerId
            && state.LastSequence.Value.BattleHostEpoch == action.BattleHostEpoch
            && state.LastSequence.Value.Sequence >= action.Sequence;
    }

    private bool HasPendingRemoteActionAtOrAfter(
        Guid agentId,
        RemoteAction action)
    {
        return _agentStates.TryGetValue(agentId, out RemoteAgentActionState state)
            && state.PendingByController != null
            && state.PendingByController.TryGetValue(
                action.ControllerId,
                out RemoteAction pending)
            && pending.BattleHostEpoch == action.BattleHostEpoch
            && pending.Sequence >= action.Sequence;
    }

    private void RecordRemoteActionSequence(Guid agentId, RemoteAction action)
    {
        GetOrCreateAgentState(agentId).LastSequence =
            new RemoteActionSequence(
                action.ControllerId,
                action.Sequence,
                action.BattleHostEpoch);
    }

    private void RemovePendingRemoteAction(Guid agentId, RemoteAction action)
    {
        if (!_agentStates.TryGetValue(agentId, out RemoteAgentActionState state)
            || state.PendingByController == null)
        {
            return;
        }

        if (state.PendingByController.TryGetValue(
            action.ControllerId,
            out RemoteAction pending)
            && pending.BattleHostEpoch == action.BattleHostEpoch)
        {
            state.PendingByController.Remove(action.ControllerId);
        }
        if (state.PendingByController.Count == 0)
        {
            state.PendingByController = null;
            _pendingActionAgentIds.Remove(agentId);
        }
        RemoveAgentStateIfEmpty(agentId, state);
    }

    private void RemoveAllPendingRemoteActions(Guid agentId)
    {
        if (!_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
            return;

        state.PendingByController = null;
        _pendingActionAgentIds.Remove(agentId);
        RemoveAgentStateIfEmpty(agentId, state);
    }

    private void RemoveExpiredPendingActions(
        Dictionary<string, RemoteAction> actionsByController,
        string currentAuthority,
        int requiredHostEpoch,
        int appliedMigrationEpoch)
    {
        List<string> expiredControllers = null;
        foreach (var pendingByController in actionsByController)
        {
            RemoteAction pending = pendingByController.Value;
            if (pending.BattleHostEpoch > 0
                && pending.BattleHostEpoch < Volatile.Read(
                    ref _highestReceivedHostActionEpoch))
            {
                (expiredControllers ??= new List<string>())
                    .Add(pendingByController.Key);
                continue;
            }

            bool isCurrentAuthority = pending.ControllerId == currentAuthority
                && (requiredHostEpoch == 0
                    || pending.BattleHostEpoch == requiredHostEpoch);
            if (isCurrentAuthority
                || pending.BattleHostEpoch > appliedMigrationEpoch)
            {
                continue;
            }

            (expiredControllers ??= new List<string>()).Add(pendingByController.Key);
        }

        if (expiredControllers == null) return;
        foreach (string controllerId in expiredControllers)
            actionsByController.Remove(controllerId);
    }

    private bool IsCurrentActionAuthority(
        CoopAgentInfo info,
        string controllerId,
        int battleHostEpoch)
    {
        if (battleHostEpoch > 0
            && battleHostEpoch < Volatile.Read(
                ref _highestReceivedHostActionEpoch))
        {
            return false;
        }

        string authority = GetCurrentActionAuthority(
            info,
            out int requiredHostEpoch);
        return authority == controllerId
            && (requiredHostEpoch != 0
                ? requiredHostEpoch == battleHostEpoch
                : battleHostEpoch == 0
                    || IsCurrentBattleHostGeneration(
                        controllerId,
                        battleHostEpoch));
    }

    private string GetCurrentActionAuthority(
        CoopAgentInfo info,
        out int requiredHostEpoch)
    {
        requiredHostEpoch = 0;
        if (_agentStates.TryGetValue(
            info.AgentId,
            out RemoteAgentActionState state)
            && state.MigratedAuthority.HasValue)
        {
            MigratedActionAuthority migrated =
                state.MigratedAuthority.Value;
            if (migrated.ObservedAuthority == info.CurrentAuthority)
            {
                requiredHostEpoch = migrated.BattleHostEpoch;
                return migrated.ControllerId;
            }

            state.MigratedAuthority = null;
        }

        return info.CurrentAuthority;
    }

    private bool IsCurrentBattleHostGeneration(
        string controllerId,
        int battleHostEpoch)
    {
        string mapEventId = BattleSpawnGate.ActiveMapEventId;
        return mapEventId != null
            && battleHostRegistry.TryGet(mapEventId, out var assignment)
            && assignment.HostControllerId == controllerId
            && assignment.Epoch == battleHostEpoch;
    }

    private bool ShouldBufferForHostAssignment(AgentActionPacket packet)
    {
        if (packet.BattleHostEpoch <= 0)
            return false;

        string mapEventId = BattleSpawnGate.ActiveMapEventId;
        if (mapEventId == null)
            return false;
        if (!battleHostRegistry.TryGet(mapEventId, out var assignment))
            return true;
        if (packet.BattleHostEpoch > assignment.Epoch)
            return true;

        return packet.BattleHostEpoch == assignment.Epoch
            && packet.ControllerId == assignment.HostControllerId;
    }

    private void ObserveHostActionEpoch(int battleHostEpoch)
    {
        if (battleHostEpoch <= 0) return;

        int observed = Volatile.Read(ref _highestReceivedHostActionEpoch);
        while (battleHostEpoch > observed)
        {
            int previous = Interlocked.CompareExchange(
                ref _highestReceivedHostActionEpoch,
                battleHostEpoch,
                observed);
            if (previous == observed)
                return;
            observed = previous;
        }
    }

    private RemoteAgentActionState GetOrCreateAgentState(Guid agentId)
    {
        if (_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
            return state;

        state = new RemoteAgentActionState();
        _agentStates[agentId] = state;
        return state;
    }

    private void RemoveAgentStateIfEmpty(
        Guid agentId,
        RemoteAgentActionState state)
    {
        if (state.IsEmpty)
            _agentStates.Remove(agentId);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _agentStates.Clear();
        _pendingActionAgentIds.Clear();
        _retainedGuardAgentIds.Clear();
        _migrationLineages.Clear();
        lock (_knownBattleHostControllersGate)
        {
            _knownBattleHostControllers.Clear();
        }
    }
}
