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
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IRemoteAgentActionProcessor : IDisposable
{
    int GetOutgoingBattleHostEpoch();
    void ClearForLocalAgent(Guid agentId, Agent agent);
    void ApplyRemoteGuardStates();
    void ReplayRemoteGuardReactions();
    void Receive(AgentActionPacket packet);
    void HandleBattleHostAssigned(NetworkBattleHostAssigned message);
}

public class RemoteAgentActionProcessor : IRemoteAgentActionProcessor
{
    private const float RetainedGuardReleaseBlendPeriod = 0.4f;

    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry battleHostRegistry;
    private readonly IMissionContext missionContext;
    private readonly IAgentVisualActionAccessor agentVisualActionAccessor;

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

    private struct RemoteGuardState
    {
        public readonly RemoteAction Action;
        public readonly int GuardActionChannel;
        public readonly ActionIndexCache GuardAction;
        public readonly ActionIndexCache DisplacedGuardAction;
        public readonly bool DrivesMountedReactionPresentation;
        public bool HasGuardCommand;
        public bool NeedsGuardDirectionTransition;
        public bool NeedsGuardPresentationTransition;
        public bool CanRecoverMissingGuardDirection;
        public bool GuardCommandAppliedWithAction;
        public Agent.GuardMode LastCommandedGuardMode;
        public int LastCommandedMountIndex;

        public RemoteGuardState(
            RemoteAction action,
            int guardActionChannel,
            ActionIndexCache guardAction,
            RemoteGuardState? previousGuard,
            bool needsGuardDirectionTransition)
        {
            Action = action;
            GuardActionChannel = guardActionChannel;
            GuardAction = guardAction;
            DisplacedGuardAction = GetDisplacedGuardAction(
                guardActionChannel,
                guardAction,
                previousGuard);
            DrivesMountedReactionPresentation =
                action.Data.IsPlayerControlled
                && action.Data.GuardActionIsReaction
                && GetMountedGuardPresentationChannel(action.Data) >= 0;
            HasGuardCommand = DrivesMountedReactionPresentation
                ? false
                : previousGuard?.HasGuardCommand ?? false;
            NeedsGuardDirectionTransition =
                !DrivesMountedReactionPresentation
                && needsGuardDirectionTransition;
            NeedsGuardPresentationTransition =
                !DrivesMountedReactionPresentation
                && action.Data.IsMounted
                && action.Data.IsPlayerControlled
                && action.Data.GuardActionIsDefending
                && !action.Data.GuardActionIsReaction
                && AgentActionData.IsGuardMode(action.Data.GuardMode)
                && guardAction != ActionIndexCache.act_none
                && previousGuard.HasValue
                && previousGuard.Value.GuardActionChannel
                    == guardActionChannel
                && previousGuard.Value.GuardAction
                    != ActionIndexCache.act_none
                && previousGuard.Value.GuardAction != guardAction;
            CanRecoverMissingGuardDirection =
                !DrivesMountedReactionPresentation
                && previousGuard.HasValue
                && previousGuard.Value.GuardActionChannel
                    == guardActionChannel
                && previousGuard.Value.GuardAction == guardAction
                && previousGuard.Value.CanRecoverMissingGuardDirection;
            GuardCommandAppliedWithAction = false;
            LastCommandedGuardMode = previousGuard?.LastCommandedGuardMode
                ?? Agent.GuardMode.None;
            LastCommandedMountIndex = previousGuard?.LastCommandedMountIndex
                ?? -1;
        }

        public RemoteGuardState(
            RemoteAction action,
            RemoteGuardState retainedGuard)
        {
            Action = action;
            GuardActionChannel = retainedGuard.GuardActionChannel;
            GuardAction = retainedGuard.GuardAction;
            DisplacedGuardAction = retainedGuard.DisplacedGuardAction;
            DrivesMountedReactionPresentation =
                action.Data.IsPlayerControlled
                && retainedGuard.DrivesMountedReactionPresentation;
            HasGuardCommand = retainedGuard.HasGuardCommand;
            NeedsGuardDirectionTransition =
                retainedGuard.NeedsGuardDirectionTransition;
            NeedsGuardPresentationTransition =
                retainedGuard.NeedsGuardPresentationTransition;
            CanRecoverMissingGuardDirection =
                retainedGuard.CanRecoverMissingGuardDirection;
            GuardCommandAppliedWithAction = false;
            LastCommandedGuardMode = retainedGuard.LastCommandedGuardMode;
            LastCommandedMountIndex = retainedGuard.LastCommandedMountIndex;
        }
    }

    private static ActionIndexCache GetDisplacedGuardAction(
        int guardActionChannel,
        ActionIndexCache guardAction,
        RemoteGuardState? previousGuard)
    {
        if (!previousGuard.HasValue)
            return ActionIndexCache.act_none;

        RemoteGuardState retainedGuard = previousGuard.Value;
        if (retainedGuard.GuardAction != ActionIndexCache.act_none
            && (guardAction == ActionIndexCache.act_none
                || (retainedGuard.GuardActionChannel == guardActionChannel
                    && retainedGuard.GuardAction != guardAction)))
        {
            return retainedGuard.GuardAction;
        }

        return retainedGuard.DisplacedGuardAction;
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
        IMissionContext missionContext,
        IAgentVisualActionAccessor agentVisualActionAccessor)
    {
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.battleHostRegistry = battleHostRegistry;
        this.missionContext = missionContext;
        this.agentVisualActionAccessor = agentVisualActionAccessor;
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
            ClearRemoteDefendState(agent, state.RetainedGuard);
        }
    }

    public void ApplyRemoteGuardStates()
    {
        if (_disposed || Mission.Current == null) return;

        ApplyPendingRemoteActions();
        ApplyRetainedRemoteGuardStates(
            replayGuardAction: false);
    }

    public void ReplayRemoteGuardReactions()
    {
        if (_disposed || Mission.Current == null) return;

        ApplyRetainedRemoteGuardStates(
            replayGuardAction: true);
    }

    private void ApplyRetainedRemoteGuardStates(
        bool replayGuardAction)
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
                RemoteGuardState guardState = state.RetainedGuard.Value;
                if (!IsCurrentActionAuthority(
                    info,
                    guardState.Action.ControllerId,
                    guardState.Action.BattleHostEpoch))
                {
                    ClearRemoteDefendState(agent, guardState);
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (replayGuardAction)
                {
                    RestoreMountedGuardDirectionPresentation(
                        agent,
                        ref guardState);
                }

                if (!replayGuardAction)
                {
                    AgentActionData data = guardState.Action.Data;
                    ApplyRetainedGuardCommand(
                        agent,
                        ref guardState,
                        restoreNativeGuardState:
                            data.IsPlayerControlled || !agent.HasMount);
                    AgentActionData.ApplyDefendMovementFlags(
                        agent,
                        data.DefendFlags);
                }

                bool hasGuardReaction =
                    replayGuardAction
                    && HasGuardReactionAction(agent, guardState);
                if (hasGuardReaction)
                {
                    guardState.HasGuardCommand = false;
                }
                state.RetainedGuard = guardState;
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

        _agentStates.TryGetValue(
            agentId,
            out RemoteAgentActionState existingState);
        int guardActionChannel =
            GetGuardActionChannel(action.Data);
        ActionIndexCache guardAction = guardActionChannel == 0
            ? new ActionIndexCache(action.Data.Action0Index)
            : guardActionChannel == 1
                ? new ActionIndexCache(action.Data.Action1Index)
                : ActionIndexCache.act_none;
        bool hasMountedGuardPresentation =
            action.Data.IsPlayerControlled
            && GetMountedGuardPresentationChannel(action.Data) >= 0;
        RemoteGuardState? previousGuard = existingState?.RetainedGuard;
        bool needsGuardDirectionTransition =
            NeedsMountedGuardDirectionTransition(
                agent,
                action.Data,
                guardActionChannel,
                guardAction,
                previousGuard);
        bool mountedGuardDirectionTransitionApplied = action.Data.Apply(
            agent,
            agentVisualActionAccessor);
        if (mountedGuardDirectionTransitionApplied)
        {
            needsGuardDirectionTransition = false;
        }

        bool retainsGuard = action.Data.DefendFlags != Agent.MovementControlFlag.None
            || AgentActionData.IsGuardMode(action.Data.GuardMode)
            || (hasMountedGuardPresentation
                && guardAction != ActionIndexCache.act_none);
        RemoteGuardState appliedGuardState;
        if (retainsGuard
            && guardActionChannel < 0
            && previousGuard.HasValue
            && previousGuard.Value.Action.Data.GuardMode
                == action.Data.GuardMode
            && previousGuard.Value.Action.Data.IsMounted
                == action.Data.IsMounted
            && previousGuard.Value.Action.Data.IsPlayerControlled
                == action.Data.IsPlayerControlled
            && !previousGuard.Value.DrivesMountedReactionPresentation)
        {
            appliedGuardState = new RemoteGuardState(
                action,
                previousGuard.Value);
        }
        else
        {
            appliedGuardState = new RemoteGuardState(
                action,
                guardActionChannel,
                guardAction,
                previousGuard,
                needsGuardDirectionTransition);
        }
        if (mountedGuardDirectionTransitionApplied)
        {
            appliedGuardState.HasGuardCommand = true;
            appliedGuardState.NeedsGuardDirectionTransition = false;
            appliedGuardState.CanRecoverMissingGuardDirection = false;
            appliedGuardState.GuardCommandAppliedWithAction = true;
            appliedGuardState.LastCommandedGuardMode =
                action.Data.GuardMode;
            appliedGuardState.LastCommandedMountIndex =
                agent.MountAgent?.Index ?? -1;
        }
        RecordRemoteActionSequence(agentId, action);
        UpdateRemoteGuardState(
            agentId,
            appliedGuardState,
            agent);

        return RemoteActionApplyResult.Applied;
    }

    private bool NeedsMountedGuardDirectionTransition(
        Agent agent,
        AgentActionData data,
        int guardActionChannel,
        ActionIndexCache guardAction,
        RemoteGuardState? previousGuard)
    {
        if (!previousGuard.HasValue)
        {
            return false;
        }

        RemoteGuardState retainedGuard = previousGuard.Value;
        if (!retainedGuard.HasGuardCommand
            || retainedGuard.LastCommandedGuardMode != data.GuardMode)
        {
            return false;
        }

        return IsMountedGuardDirectionMissing(
            agent,
            data,
            guardActionChannel,
            guardAction,
            GetDisplacedGuardAction(
                guardActionChannel,
                guardAction,
                previousGuard));
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
        RemoteGuardState guardState,
        Agent agent)
    {
        Agent.MovementControlFlag defendFlags = guardState.Action.Data.DefendFlags;
        Agent.GuardMode guardMode = guardState.Action.Data.GuardMode;

        if (defendFlags != Agent.MovementControlFlag.None
            || AgentActionData.IsGuardMode(guardMode)
            || (guardState.DrivesMountedReactionPresentation
                && guardState.GuardAction != ActionIndexCache.act_none))
        {
            RemoteAgentActionState retainedState = GetOrCreateAgentState(agentId);
            retainedState.RetainedGuard = guardState;
            _retainedGuardAgentIds.Add(agentId);
            return;
        }

        RemoteGuardState? releasedGuard = null;
        if (_agentStates.TryGetValue(agentId, out RemoteAgentActionState state))
        {
            releasedGuard = state.RetainedGuard;
            state.RetainedGuard = null;
            RemoveAgentStateIfEmpty(agentId, state);
        }
        _retainedGuardAgentIds.Remove(agentId);
        ClearRemoteDefendState(agent, releasedGuard);
    }

    private void ApplyRetainedGuardCommand(
        Agent agent,
        ref RemoteGuardState guardState,
        bool restoreNativeGuardState)
    {
        Agent.GuardMode guardMode = guardState.Action.Data.GuardMode;
        if (!AgentActionData.IsGuardMode(guardMode))
        {
            return;
        }

        if (HasInterruptingGuardAction(agent, guardState))
        {
            guardState.HasGuardCommand = false;
            return;
        }

        if (HasGuardReactionAction(agent, guardState)
            || guardState.DrivesMountedReactionPresentation)
        {
            return;
        }

        int mountIndex = agent.MountAgent?.Index ?? -1;
        bool reacquiringGuard = !guardState.HasGuardCommand
            && AgentActionData.IsGuardMode(
                guardState.LastCommandedGuardMode);
        bool mountChanged = guardState.HasGuardCommand
            && guardState.LastCommandedMountIndex != mountIndex;
        bool guardModeChanged = guardState.HasGuardCommand
            && guardState.LastCommandedGuardMode != guardMode;
        bool tracksMountedGuardDirection =
            agent.HasMount
            && guardState.Action.Data.IsMounted
            && guardState.Action.Data.IsPlayerControlled
            && guardState.Action.Data.GuardActionIsDefending
            && !guardState.Action.Data.GuardActionIsReaction
            && guardState.GuardActionChannel >= 0
            && guardState.GuardActionChannel <= 1
            && guardState.GuardAction != ActionIndexCache.act_none;
        bool mountedGuardDirectionMissing =
            tracksMountedGuardDirection
            && IsMountedGuardDirectionMissing(
                agent,
                guardState.Action.Data,
                guardState.GuardActionChannel,
                guardState.GuardAction,
                guardState.DisplacedGuardAction);
        if (tracksMountedGuardDirection
            && !mountedGuardDirectionMissing)
        {
            guardState.CanRecoverMissingGuardDirection = true;
        }
        bool missingGuardDirectionRecovery =
            mountedGuardDirectionMissing
            && guardState.CanRecoverMissingGuardDirection;
        // A steady-state defending sibling needs a direction repair without restarting its timeline.
        bool missingGuardDirectionRequiresNativeCommand =
            missingGuardDirectionRecovery
            && (mountChanged
                || reacquiringGuard
                || agent.GetCurrentAction(
                    guardState.GuardActionChannel) == guardState.GuardAction
                || !AgentActionData.IsDefendingAction(
                    agent.GetCurrentActionType(
                        guardState.GuardActionChannel)));
        bool directionTransitionPending =
            guardState.NeedsGuardDirectionTransition
            || missingGuardDirectionRecovery;
        bool refreshMountedGuardCommand =
            tracksMountedGuardDirection
            && guardState.HasGuardCommand
            && !guardState.GuardCommandAppliedWithAction
            && !directionTransitionPending
            && !mountChanged
            && !guardModeChanged;
        guardState.GuardCommandAppliedWithAction = false;
        bool nativeGuardStateMissing =
            restoreNativeGuardState
            && !agent.HasMount
            && agent.CurrentGuardMode != guardMode
            && !HasDefendingAction(agent);

        if (!guardState.HasGuardCommand
            || mountChanged
            || guardModeChanged
            || directionTransitionPending
            || refreshMountedGuardCommand
            || nativeGuardStateMissing)
        {
            if (agent.HasMount
                && (guardModeChanged
                    || guardState.NeedsGuardDirectionTransition
                    || missingGuardDirectionRequiresNativeCommand))
            {
                AgentActionData.ApplyGuardDirectionTransition(
                    agent,
                    guardMode);
                guardState.CanRecoverMissingGuardDirection = false;
                if (missingGuardDirectionRecovery)
                {
                    guardState.NeedsGuardPresentationTransition = true;
                }
            }
            else if (missingGuardDirectionRecovery)
            {
                AgentActionData.ApplyGuardState(
                    agent,
                    guardMode,
                    force: true);
                guardState.CanRecoverMissingGuardDirection = false;
                guardState.NeedsGuardPresentationTransition = true;
            }
            else
            {
                bool forceGuardCommand =
                    mountChanged
                    || reacquiringGuard
                    || refreshMountedGuardCommand
                    || nativeGuardStateMissing;
                AgentActionData.ApplyGuardState(
                    agent,
                    guardMode,
                    force: forceGuardCommand);
            }
        }

        guardState.HasGuardCommand = true;
        guardState.NeedsGuardDirectionTransition = false;
        guardState.LastCommandedGuardMode = guardMode;
        guardState.LastCommandedMountIndex = mountIndex;
    }

    private void RestoreMountedGuardDirectionPresentation(
        Agent agent,
        ref RemoteGuardState guardState)
    {
        if (!guardState.NeedsGuardPresentationTransition
            || guardState.DrivesMountedReactionPresentation
            || HasGuardReactionAction(agent, guardState)
            || HasInterruptingGuardAction(agent, guardState))
        {
            return;
        }

        int channel = guardState.GuardActionChannel;
        if (agentVisualActionAccessor.IsActionVisible(
            agent,
            channel,
            in guardState.GuardAction))
        {
            if (guardState.CanRecoverMissingGuardDirection)
            {
                guardState.NeedsGuardPresentationTransition = false;
            }
            return;
        }

        if (agent.GetCurrentAction(channel) == guardState.GuardAction)
            return;

        if (!IsMountedGuardDirectionMissing(
                agent,
                guardState.Action.Data,
                channel,
                guardState.GuardAction,
                guardState.DisplacedGuardAction))
        {
            return;
        }

        AgentActionData data = guardState.Action.Data;
        AnimFlags actionFlags = (AnimFlags)(channel == 0
            ? data.Action0Flag
            : data.Action1Flag);
        float actionProgress = channel == 0
            ? data.Action0Progress
            : data.Action1Progress;
        agent.SetActionChannel(
            channel,
            guardState.GuardAction,
            ignorePriority: true,
            additionalFlags: actionFlags | AnimFlags.anf_restart,
            startProgress: actionProgress);
        if (agentVisualActionAccessor.IsActionVisible(
            agent,
            channel,
            in guardState.GuardAction)
            && guardState.CanRecoverMissingGuardDirection)
        {
            guardState.NeedsGuardPresentationTransition = false;
        }
    }

    private bool IsMountedGuardDirectionMissing(
        Agent agent,
        AgentActionData data,
        int channel,
        ActionIndexCache guardAction,
        ActionIndexCache displacedGuardAction)
    {
        if (!agent.HasMount
            || !data.IsMounted
            || !data.IsPlayerControlled
            || !data.GuardActionIsDefending
            || data.GuardActionIsReaction
            || channel < 0
            || channel > 1
            || !AgentActionData.IsGuardMode(data.GuardMode))
        {
            return false;
        }

        ActionIndexCache currentAction = agent.GetCurrentAction(channel);
        if (currentAction == ActionIndexCache.act_none
            && agentVisualActionAccessor.IsActionVisible(
                agent,
                channel,
                in guardAction))
        {
            return false;
        }
        if (currentAction == ActionIndexCache.act_none
            && displacedGuardAction != ActionIndexCache.act_none
            && agentVisualActionAccessor.IsActionVisible(
                agent,
                channel,
                in displacedGuardAction))
        {
            return true;
        }
        if (currentAction == ActionIndexCache.act_none
            && agentVisualActionAccessor.HasVisibleAction(
                agent,
                channel))
        {
            return false;
        }
        if (currentAction != ActionIndexCache.act_none
            && !AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(channel)))
        {
            return false;
        }

        Agent.GuardMode currentGuardMode =
            AgentActionData.GetGuardModeFromDefendingAction(
                agent,
                channel);
        return currentGuardMode != data.GuardMode;
    }

    private static bool HasInterruptingGuardAction(
        Agent agent,
        in RemoteGuardState guardState)
    {
        if (HasGuardReactionAction(agent, guardState))
            return false;

        return IsInterruptingGuardAction(
                agent.GetCurrentActionType(0))
            || IsInterruptingGuardAction(
                agent.GetCurrentActionType(1));
    }

    private static bool HasGuardReactionAction(
        Agent agent,
        in RemoteGuardState guardState) =>
        IsGuardReactionAction(agent, 0, guardState)
        || IsGuardReactionAction(agent, 1, guardState);

    private static bool IsGuardReactionAction(
        Agent agent,
        int channel,
        in RemoteGuardState guardState)
    {
        Agent.ActionCodeType actionType =
            agent.GetCurrentActionType(channel);
        if (AgentActionData.IsGuardReactionAction(actionType))
            return true;

        ActionIndexCache action = agent.GetCurrentAction(channel);
        return action != ActionIndexCache.act_none
            && (channel != guardState.GuardActionChannel
                || action != guardState.GuardAction)
            && AgentActionData.IsDefendingAction(actionType)
            && agent.GetCurrentActionStage(channel)
                == Agent.ActionStage.DefendParry;
    }

    private static bool HasDefendingAction(Agent agent) =>
        AgentActionData.IsDefendingAction(agent.GetCurrentActionType(0))
        || AgentActionData.IsDefendingAction(agent.GetCurrentActionType(1));

    private static bool IsInterruptingGuardAction(
        Agent.ActionCodeType actionType) =>
        actionType != Agent.ActionCodeType.Other
        && actionType != Agent.ActionCodeType.Idle
        && !AgentActionData.IsDefendingAction(actionType)
        && !AgentActionData.IsGuardReactionAction(actionType);

    private void ClearRetainedGuardAction(
        Agent agent,
        RemoteGuardState? retainedGuard)
    {
        if (!retainedGuard.HasValue) return;

        RemoteGuardState guardState = retainedGuard.Value;
        int channel = guardState.GuardActionChannel;
        if (channel < 0 || channel > 1) return;

        ActionIndexCache currentAction = agent.GetCurrentAction(channel);
        bool ownsAgentAction = currentAction == guardState.GuardAction;
        if (!ownsAgentAction && currentAction != ActionIndexCache.act_none)
            return;

        if (!ownsAgentAction
            && !agentVisualActionAccessor.IsActionVisible(
                agent,
                channel,
                guardState.GuardAction))
        {
            return;
        }

        // Only clear the action this retained guard owns; leave reactions and attacks alone.
        agent.SetActionChannel(
            channel,
            ActionIndexCache.act_none,
            ignorePriority: true,
            additionalFlags: AnimFlags.anf_restart,
            blendInPeriod: RetainedGuardReleaseBlendPeriod,
            forceFaceMorphRestart: false);
    }

    private void ClearRemoteDefendState(
        Agent agent,
        RemoteGuardState? retainedGuard = null)
    {
        ClearRetainedGuardAction(agent, retainedGuard);
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            Agent.MovementControlFlag.None);
        AgentActionData.ApplyGuardState(agent, Agent.GuardMode.None);
    }

    private static int GetMountedGuardPresentationChannel(
        AgentActionData data)
    {
        if (!data.IsMounted)
            return -1;

        int channel = data.GuardPresentationChannel;
        return channel >= 0 && channel <= 1 ? channel : -1;
    }

    private static int GetGuardActionChannel(AgentActionData data)
    {
        int channel = data.GuardActionChannel;
        return channel >= 0 && channel <= 1 ? channel : -1;
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
