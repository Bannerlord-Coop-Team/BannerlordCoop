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
    void RefreshRemoteGuardStatesAfterMovement();
    void AdvanceRemoteGuardStatesAfterNativeTick();
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
        public RemoteGuardState RetainedGuard;
        public RemoteActionSequence? LastSequence;
        public Dictionary<string, RemoteAction> PendingByController;
        public MigratedActionAuthority? MigratedAuthority;

        public bool IsEmpty =>
            RetainedGuard == null
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

    private enum MountedGuardRearmPhase
    {
        None,
        NeutralInput,
        NeutralInputObserved,
        DesiredInput
    }

    private enum RemoteGuardTickPhase
    {
        BeforeNativeTick,
        AfterMovement,
        AfterNativeTick
    }

    private sealed class RemoteGuardState
    {
        public readonly RemoteAction Action;
        public readonly int GuardActionChannel;
        public readonly ActionIndexCache GuardAction;
        public readonly bool DrivesMountedReactionPresentation;
        public bool HasGuardCommand;
        public MountedGuardRearmPhase MountedGuardRearm;
        public Agent.GuardMode LastCommandedGuardMode;
        public int LastCommandedMountIndex;

        public RemoteGuardState(
            RemoteAction action,
            int guardActionChannel,
            ActionIndexCache guardAction,
            RemoteGuardState previousGuard)
        {
            Action = action;
            GuardActionChannel = guardActionChannel;
            GuardAction = guardAction;
            DrivesMountedReactionPresentation =
                action.Data.IsPlayerControlled
                && action.Data.GuardActionIsReaction
                && GetMountedGuardPresentationChannel(action.Data) >= 0;
            HasGuardCommand = DrivesMountedReactionPresentation
                ? false
                : previousGuard?.HasGuardCommand ?? false;
            MountedGuardRearm = MountedGuardRearmPhase.None;
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
            DrivesMountedReactionPresentation =
                action.Data.IsPlayerControlled
                && retainedGuard.DrivesMountedReactionPresentation;
            HasGuardCommand = retainedGuard.HasGuardCommand;
            MountedGuardRearm = retainedGuard.MountedGuardRearm;
            LastCommandedGuardMode = retainedGuard.LastCommandedGuardMode;
            LastCommandedMountIndex = retainedGuard.LastCommandedMountIndex;
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
        if (state.RetainedGuard == null) return;
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
            RemoteGuardTickPhase.BeforeNativeTick);
    }

    public void RefreshRemoteGuardStatesAfterMovement()
    {
        if (_disposed || Mission.Current == null) return;

        ApplyRetainedRemoteGuardStates(
            RemoteGuardTickPhase.AfterMovement);
    }

    public void AdvanceRemoteGuardStatesAfterNativeTick()
    {
        if (_disposed || Mission.Current == null) return;

        ApplyRetainedRemoteGuardStates(
            RemoteGuardTickPhase.AfterNativeTick);
    }

    private void ApplyRetainedRemoteGuardStates(
        RemoteGuardTickPhase phase)
    {
        if (_disposed || Mission.Current == null) return;
        if (_retainedGuardAgentIds.Count == 0) return;

        bool afterMovement =
            phase == RemoteGuardTickPhase.AfterMovement;
        bool afterNativeTick =
            phase == RemoteGuardTickPhase.AfterNativeTick;
        List<Guid> staleIds = null;
        using (new AllowedThread())
        {
            foreach (Guid agentId in _retainedGuardAgentIds)
            {
                if (!_agentStates.TryGetValue(
                    agentId,
                    out RemoteAgentActionState state)
                    || state.RetainedGuard == null)
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
                RemoteGuardState guardState = state.RetainedGuard;
                if (!IsCurrentActionAuthority(
                    info,
                    guardState.Action.ControllerId,
                    guardState.Action.BattleHostEpoch))
                {
                    ClearRemoteDefendState(agent, guardState);
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (guardState.MountedGuardRearm
                    != MountedGuardRearmPhase.None)
                {
                    ApplyMountedGuardRearm(
                        agent,
                        guardState,
                        phase);
                    continue;
                }

                if (!afterNativeTick)
                {
                    AgentActionData data = guardState.Action.Data;
                    AgentActionData.ApplyDefendMovementFlags(
                        agent,
                        data.DefendFlags);
                    if (!afterMovement)
                    {
                        ApplyRetainedGuardCommand(
                            agent,
                            guardState,
                            restoreNativeGuardState:
                                data.IsPlayerControlled || !agent.HasMount);
                    }
                }

                bool hasGuardReaction =
                    afterNativeTick
                    && HasGuardReactionAction(agent, guardState);
                if (hasGuardReaction)
                {
                    guardState.HasGuardCommand = false;
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
        RemoteGuardState previousGuard = existingState?.RetainedGuard;
        MountedGuardRearmPhase mountedGuardRearm =
            GetMountedGuardRearmPhase(
                agent,
                action.Data,
                guardActionChannel,
                previousGuard);
        action.Data.Apply(
            agent,
            agentVisualActionAccessor,
            suppressMountedGuardActionTransition:
                mountedGuardRearm != MountedGuardRearmPhase.None,
            neutralizeMountedGuardDirection:
                mountedGuardRearm
                    == MountedGuardRearmPhase.NeutralInput
                || mountedGuardRearm
                    == MountedGuardRearmPhase.NeutralInputObserved);
        bool retainsGuard = action.Data.DefendFlags != Agent.MovementControlFlag.None
            || AgentActionData.IsGuardMode(action.Data.GuardMode)
            || (hasMountedGuardPresentation
                && guardAction != ActionIndexCache.act_none);
        RemoteGuardState appliedGuardState;
        if (retainsGuard
            && guardActionChannel < 0
            && previousGuard != null
            && previousGuard.Action.Data.GuardMode
                == action.Data.GuardMode
            && previousGuard.Action.Data.IsMounted
                == action.Data.IsMounted
            && previousGuard.Action.Data.IsPlayerControlled
                == action.Data.IsPlayerControlled
            && !previousGuard.DrivesMountedReactionPresentation)
        {
            appliedGuardState = new RemoteGuardState(
                action,
                previousGuard);
        }
        else
        {
            appliedGuardState = new RemoteGuardState(
                action,
                guardActionChannel,
                guardAction,
                previousGuard);
        }
        appliedGuardState.MountedGuardRearm = mountedGuardRearm;
        if (mountedGuardRearm != MountedGuardRearmPhase.None)
        {
            appliedGuardState.HasGuardCommand = false;
        }
        RecordRemoteActionSequence(agentId, action);
        UpdateRemoteGuardState(
            agentId,
            appliedGuardState,
            agent);

        return RemoteActionApplyResult.Applied;
    }

    private static MountedGuardRearmPhase GetMountedGuardRearmPhase(
        Agent agent,
        AgentActionData data,
        int guardActionChannel,
        RemoteGuardState previousGuard)
    {
        if (agent.Controller != AgentControllerType.None
            || !agent.HasMount
            || !data.IsMounted
            || !data.IsPlayerControlled
            || data.GuardActionIsReaction
            || !AgentActionData.IsGuardMode(data.GuardMode)
            || (data.DefendFlags
                & Agent.MovementControlFlag.DefendBlock) == 0
            || AgentActionData.GetGuardModeFromDefendFlags(
                data.DefendFlags) != data.GuardMode
            || AgentActionData.IsGuardReactionAction(
                agent.GetCurrentActionType(0))
            || AgentActionData.IsGuardReactionAction(
                agent.GetCurrentActionType(1)))
        {
            return MountedGuardRearmPhase.None;
        }

        Agent.GuardMode currentGuardMode =
            guardActionChannel >= 0 && guardActionChannel <= 1
                ? AgentActionData.GetGuardModeFromDefendingAction(
                    agent,
                    guardActionChannel)
                : AgentActionData.GetGuardModeFromDefendingAction(agent);
        if (currentGuardMode == data.GuardMode)
            return MountedGuardRearmPhase.None;

        if (previousGuard != null
            && previousGuard.MountedGuardRearm
                != MountedGuardRearmPhase.None
            && previousGuard.Action.Data.GuardMode
                == data.GuardMode)
        {
            return previousGuard.MountedGuardRearm;
        }

        if (!data.GuardActionIsDefending)
            return MountedGuardRearmPhase.None;

        Agent.GuardMode previousGuardMode =
            previousGuard?.Action.Data.GuardMode
                ?? Agent.GuardMode.None;
        if ((AgentActionData.IsGuardMode(currentGuardMode)
                && currentGuardMode != data.GuardMode)
            || (AgentActionData.IsGuardMode(previousGuardMode)
                && previousGuardMode != data.GuardMode))
        {
            // None-controlled mounted puppets accept the opposite guard sibling
            // after one directionless input cycle.
            return MountedGuardRearmPhase.NeutralInput;
        }

        return MountedGuardRearmPhase.None;
    }

    private static void ApplyMountedGuardRearm(
        Agent agent,
        RemoteGuardState guardState,
        RemoteGuardTickPhase phase)
    {
        AgentActionData data = guardState.Action.Data;
        if (phase == RemoteGuardTickPhase.AfterNativeTick)
        {
            if (guardState.MountedGuardRearm
                == MountedGuardRearmPhase.NeutralInput)
            {
                // This replay runs after the native Agent cycle.
                guardState.MountedGuardRearm =
                    MountedGuardRearmPhase.NeutralInputObserved;
                return;
            }

            if (guardState.MountedGuardRearm
                    == MountedGuardRearmPhase.DesiredInput
                && AgentActionData.GetGuardModeFromDefendingAction(
                    agent) == data.GuardMode)
            {
                guardState.MountedGuardRearm =
                    MountedGuardRearmPhase.None;
                guardState.HasGuardCommand = true;
                guardState.LastCommandedGuardMode = data.GuardMode;
                guardState.LastCommandedMountIndex =
                    agent.MountAgent?.Index ?? -1;
            }
            return;
        }

        if (guardState.MountedGuardRearm
                == MountedGuardRearmPhase.NeutralInput
            || (guardState.MountedGuardRearm
                    == MountedGuardRearmPhase.NeutralInputObserved
                && phase != RemoteGuardTickPhase.AfterMovement))
        {
            return;
        }

        if (guardState.MountedGuardRearm
            == MountedGuardRearmPhase.NeutralInputObserved)
        {
            AgentActionData.ApplyDefendMovementFlags(
                agent,
                data.DefendFlags);
            guardState.MountedGuardRearm =
                MountedGuardRearmPhase.DesiredInput;
            return;
        }

        AgentActionData.ApplyDefendMovementFlags(
            agent,
            data.DefendFlags);
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

        RemoteGuardState releasedGuard = null;
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
        RemoteGuardState guardState,
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
        bool nativeGuardStateMissing =
            restoreNativeGuardState
            && !agent.HasMount
            && agent.CurrentGuardMode != guardMode
            && !HasDefendingAction(agent);

        bool shouldApplyGuardCommand =
            !guardState.HasGuardCommand
            || mountChanged
            || guardModeChanged
            || nativeGuardStateMissing;
        if (shouldApplyGuardCommand)
        {
            bool forceGuardCommand =
                mountChanged
                || reacquiringGuard
                || nativeGuardStateMissing;
            if (guardModeChanged)
            {
                AgentActionData.ApplyGuardDirectionTransition(
                    agent,
                    guardMode);
            }
            else
            {
                AgentActionData.ApplyGuardState(
                    agent,
                    guardMode,
                    force: forceGuardCommand);
            }
        }

        guardState.HasGuardCommand = true;
        guardState.LastCommandedGuardMode = guardMode;
        guardState.LastCommandedMountIndex = mountIndex;
    }

    private static bool HasInterruptingGuardAction(
        Agent agent,
        RemoteGuardState guardState)
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
        RemoteGuardState guardState) =>
        IsGuardReactionAction(agent, 0, guardState)
        || IsGuardReactionAction(agent, 1, guardState);

    private static bool IsGuardReactionAction(
        Agent agent,
        int channel,
        RemoteGuardState guardState)
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
        RemoteGuardState retainedGuard)
    {
        if (retainedGuard == null) return;

        int channel = retainedGuard.GuardActionChannel;
        if (channel < 0 || channel > 1) return;

        ActionIndexCache currentAction = agent.GetCurrentAction(channel);
        bool ownsAgentAction = currentAction == retainedGuard.GuardAction;
        if (!ownsAgentAction && currentAction != ActionIndexCache.act_none)
            return;

        if (!ownsAgentAction
            && !agentVisualActionAccessor.IsActionVisible(
                agent,
                channel,
                retainedGuard.GuardAction))
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
        RemoteGuardState retainedGuard = null)
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
