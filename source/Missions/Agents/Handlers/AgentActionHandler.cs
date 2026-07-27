using Common;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentActionHandler : IPacketHandler, IDisposable
{
    /// <summary>
    /// [Game thread] Detect discrete action, defend-input, and realized guard changes on owned agents and broadcast them. Driven from
    /// CoopMissionController.OnPreDisplayMissionTick after the completed native Agent tick and again from OnMissionTick after behavior input:
    /// these are the game-thread boundaries where a one-frame transition can be observed without racing the engine.
    /// </summary>
    void PollActions();

    /// <summary>[Any thread] Send held defend and guard state for owned agents to a joining peer.</summary>
    void CatchUpJoiner(string controllerId);

    /// <summary>[Game thread] Capture and apply one-shot guard reactions before the display snapshot.</summary>
    void ReplayRemoteGuardReactions();

    /// <summary>[Game thread] Record a locally authoritative blocked melee collision.</summary>
    void ObserveBlockedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult);

    /// <summary>[Game thread] Apply queued remote actions and restore retained guard state before native collision.</summary>
    void ApplyRemoteGuardStates();

    /// <summary>[Game thread] Refresh retained mounted guard direction after continuous movement replay.</summary>
    void RefreshRemoteGuardStatesAfterMovement();
}

/// <summary>
/// Event-driven animation sync. Where movement is continuous state (polled, smoothed, unreliable), an ACTION is
/// an event — "this agent started a punch/attack/jump" — that plays out locally over time. So instead of polling
/// the full action state every tick and re-applying it (which lost one-frame triggers, fought the local
/// animation, and churned the skeleton), this diffs each owned agent's action channels ON THE GAME THREAD and,
/// only when a DISCRETE action, held defend input, or realized guard state changes, broadcasts it
/// <see cref="DeliveryMethod.ReliableOrdered"/>. The receiver applies the transition and lets the engine advance
/// it. Locomotion (walk/run/idle) is skipped — the movement packet continuously supplies its input and move flags.
/// </summary>
public class AgentActionHandler : IAgentActionHandler
{
    // Reliable delivery fragments, so this is only to avoid one-giant-packet; action changes per frame are few.
    private const int MaxAgentsPerActionPacket = 8;

    private readonly IBattleNetwork client;
    private readonly IPacketManager packetManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IRemoteAgentActionProcessor remoteActionProcessor;
    private readonly IGuardReactionHandler guardReactionHandler;

    // Outbound observation and sequence share one record because both belong to the local agent's action stream.
    private readonly Dictionary<Guid, LocalAgentActionState> _localAgentStates =
        new Dictionary<Guid, LocalAgentActionState>();

    private bool _disposed;

    private struct LocalAgentActionState
    {
        public bool HasObservation;
        public int Action0;
        public int Action1;
        public Agent.MovementControlFlag DefendFlags;
        public Agent.GuardMode GuardMode;
        public Agent.GuardMode ActionGuardMode;
        public bool Action0WasDiscrete;
        public bool Action1WasDiscrete;
        public bool Action0WasDefending;
        public bool Action1WasDefending;
        public bool IsMounted;
        public bool IsPlayerControlled;
        public bool HasAction0DefendingAction;
        public bool HasAction1DefendingAction;
        public int Action0DefendingAction;
        public int Action1DefendingAction;
        public long Sequence;
    }

    public AgentActionHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IRemoteAgentActionProcessor remoteActionProcessor,
        IGuardReactionHandler guardReactionHandler)
    {
        this.client = client;
        this.packetManager = packetManager;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.remoteActionProcessor = remoteActionProcessor;
        this.guardReactionHandler = guardReactionHandler;

        this.packetManager.RegisterPacketHandler(this);
        this.messageBroker.Subscribe<NetworkBattleHostAssigned>(Handle_BattleHostAssigned);
    }

    public PacketType PacketType => PacketType.AgentAction;

    public void PollActions()
    {
        if (Mission.Current == null) return;

        List<Guid> ids = null;
        List<AgentActionData> actions = null;
        List<long> sequences = null;

        foreach (var info in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            Agent agent = info.Agent;
            remoteActionProcessor.ClearForLocalAgent(info.AgentId, agent);
            if (agent == null || agent.Mission == null || !agent.IsActive() || agent.Health <= 0)
                continue;

            // Registered MOUNTS are not action-synced: a ridden horse's channel-1 action already rides in its
            // rider's MountData (a second stream would fight it), and a masterless one isn't movement-synced
            // either — its registry entry exists for damage routing and death sync only.
            if (agent.IsMount)
                continue;

            int action0 = agent.GetCurrentAction(0).Index;
            int action1 = agent.GetCurrentAction(1).Index;
            var defendFlags = AgentActionData.GetEffectiveDefendMovementFlags(agent);

            _localAgentStates.TryGetValue(info.AgentId, out var state);
            bool hadState = state.HasObservation;
            Agent.GuardMode actionGuardMode =
                AgentActionData.GetGuardModeFromDefendingAction(agent);
            Agent.GuardMode guardMode = GetEffectiveGuardMode(
                info.AgentId,
                agent,
                defendFlags,
                actionGuardMode);
            bool defendChanged;
            if (hadState)
            {
                defendChanged = agent.HasMount
                    ? HasDefendStateChanged(
                        state.DefendFlags,
                        defendFlags,
                        state.GuardMode,
                        guardMode)
                    : state.DefendFlags != defendFlags;
            }
            else
            {
                defendChanged = defendFlags != Agent.MovementControlFlag.None;
            }

            bool guardChanged;
            if (hadState)
            {
                guardChanged = state.GuardMode != guardMode;
            }
            else
            {
                guardChanged = AgentActionData.IsGuardMode(guardMode);
            }

            bool guardedMountStateChanged =
                hadState
                && state.IsMounted != agent.HasMount
                && (defendFlags != Agent.MovementControlFlag.None
                    || AgentActionData.IsGuardMode(guardMode)
                    || state.DefendFlags != Agent.MovementControlFlag.None
                    || AgentActionData.IsGuardMode(state.GuardMode));
            bool guardedControllerRoleChanged =
                hadState
                && state.IsPlayerControlled
                    != (agent.Controller == AgentControllerType.Player)
                && (defendFlags != Agent.MovementControlFlag.None
                    || AgentActionData.IsGuardMode(guardMode)
                    || state.DefendFlags != Agent.MovementControlFlag.None
                    || AgentActionData.IsGuardMode(state.GuardMode));
            bool action0Changed = !hadState || state.Action0 != action0;
            bool action1Changed = !hadState || state.Action1 != action1;
            if (!action0Changed && !action1Changed
                && !defendChanged && !guardChanged
                && !guardedMountStateChanged
                && !guardedControllerRoleChanged)
            {
                if (hadState && agent.HasMount)
                {
                    state.DefendFlags = defendFlags;
                    state.ActionGuardMode = actionGuardMode;
                    _localAgentStates[info.AgentId] = state;
                }
                continue;
            }

            bool action0Discrete =
                IsDiscreteAction(agent.GetCurrentActionType(0));
            bool action1Discrete =
                IsDiscreteAction(agent.GetCurrentActionType(1));
            bool action0Defending = AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(0));
            bool action1Defending = AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(1));
            int guardReactionChannel = GetGuardReactionChannel(
                agent,
                hadState,
                state);

            // Native command actions are untyped, so recognize the main agent's order gesture by action name.
            if (agent == Mission.Current.MainAgent)
            {
                action0Discrete |= IsOrderGesture(
                    AgentActionData.GetActionNameWithCode(action0));
                action1Discrete |= IsOrderGesture(
                    AgentActionData.GetActionNameWithCode(action1));
            }

            bool heldMountedGuardUnchanged =
                agent.HasMount
                && hadState
                && !defendChanged
                && !guardChanged
                && (defendFlags != Agent.MovementControlFlag.None
                    || AgentActionData.IsGuardMode(guardMode));
            bool action0GuardLocomotionChurn =
                IsMountedGuardLocomotionChurn(
                    heldMountedGuardUnchanged,
                    action0Changed,
                    action0,
                    action0Discrete,
                    action0Defending,
                    state.Action0WasDefending,
                    state.HasAction0DefendingAction,
                    state.Action0DefendingAction);
            bool action1GuardLocomotionChurn =
                IsMountedGuardLocomotionChurn(
                    heldMountedGuardUnchanged,
                    action1Changed,
                    action1,
                    action1Discrete,
                    action1Defending,
                    state.Action1WasDefending,
                    state.HasAction1DefendingAction,
                    state.Action1DefendingAction);

            // Defend input and realized guard state can change before the animation index, so send them explicitly too.
            bool discreteActionChanged =
                (action0Changed
                    && !action0GuardLocomotionChurn
                    && (action0Discrete
                        || (hadState && state.Action0WasDiscrete)))
                || (action1Changed
                    && !action1GuardLocomotionChurn
                    && (action1Discrete
                        || (hadState && state.Action1WasDiscrete)));
            bool broadcast =
                defendChanged
                || guardChanged
                || guardedMountStateChanged
                || guardedControllerRoleChanged
                || discreteActionChanged;
            state.HasObservation = true;
            state.Action0 = action0;
            state.Action1 = action1;
            state.DefendFlags = defendFlags;
            state.GuardMode = guardMode;
            state.ActionGuardMode = actionGuardMode;
            state.Action0WasDiscrete = action0Discrete;
            state.Action1WasDiscrete = action1Discrete;
            state.Action0WasDefending = action0Defending;
            state.Action1WasDefending = action1Defending;
            state.IsMounted = agent.HasMount;
            state.IsPlayerControlled =
                agent.Controller == AgentControllerType.Player;
            UpdateDefendingAction(
                action0,
                agent.GetCurrentActionType(0),
                ref state.HasAction0DefendingAction,
                ref state.Action0DefendingAction);
            UpdateDefendingAction(
                action1,
                agent.GetCurrentActionType(1),
                ref state.HasAction1DefendingAction,
                ref state.Action1DefendingAction);
            if (defendFlags == Agent.MovementControlFlag.None
                && !AgentActionData.IsGuardMode(guardMode))
            {
                state.HasAction0DefendingAction = false;
                state.HasAction1DefendingAction = false;
            }
            _localAgentStates[info.AgentId] = state;
            if (!broadcast)
                continue;

            long sequence = NextActionSequence(info.AgentId);
            (ids ??= new List<Guid>()).Add(info.AgentId);
            (actions ??= new List<AgentActionData>()).Add(
                new AgentActionData(
                    agent,
                    defendFlags,
                    guardMode,
                    guardReactionChannel));
            (sequences ??= new List<long>()).Add(sequence);
        }

        if (ids == null) return;

        SendActionPackets(
            controllerIdProvider.ControllerId,
            ids,
            actions,
            sequences,
            packet => client.SendAll(packet));
    }

    public void CatchUpJoiner(string controllerId)
    {
        GameThread.RunSafe(() =>
        {
            if (_disposed || Mission.Current == null) return;

            List<Guid> ids = null;
            List<AgentActionData> actions = null;
            List<long> sequences = null;

            foreach (var info in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
            {
                Agent agent = info.Agent;
                if (agent == null || agent.Mission == null || !agent.IsActive() || agent.Health <= 0 || agent.IsMount)
                    continue;

                var defendFlags = AgentActionData.GetEffectiveDefendMovementFlags(agent);
                Agent.GuardMode actionGuardMode =
                    AgentActionData.GetGuardModeFromDefendingAction(agent);
                Agent.GuardMode guardMode = GetEffectiveGuardMode(
                    info.AgentId,
                    agent,
                    defendFlags,
                    actionGuardMode);
                if (defendFlags == Agent.MovementControlFlag.None
                    && !AgentActionData.IsGuardMode(guardMode))
                    continue;

                (ids ??= new List<Guid>()).Add(info.AgentId);
                (actions ??= new List<AgentActionData>()).Add(
                    new AgentActionData(agent, defendFlags, guardMode));
                (sequences ??= new List<long>()).Add(NextActionSequence(info.AgentId));
            }

            if (ids == null) return;
            SendActionPackets(
                controllerIdProvider.ControllerId,
                ids,
                actions,
                sequences,
                packet => client.Send(controllerId, packet));
        });
    }

    private void SendActionPackets(
        string controllerId,
        List<Guid> ids,
        List<AgentActionData> actions,
        List<long> sequences,
        Action<AgentActionPacket> send)
    {
        int battleHostEpoch = remoteActionProcessor.GetOutgoingBattleHostEpoch();
        for (int start = 0; start < ids.Count; start += MaxAgentsPerActionPacket)
        {
            int count = Math.Min(MaxAgentsPerActionPacket, ids.Count - start);
            var idChunk = new Guid[count];
            var dataChunk = new AgentActionData[count];
            var sequenceChunk = new long[count];
            ids.CopyTo(start, idChunk, 0, count);
            actions.CopyTo(start, dataChunk, 0, count);
            sequences.CopyTo(start, sequenceChunk, 0, count);
            send(new AgentActionPacket(
                controllerId,
                idChunk,
                dataChunk,
                sequenceChunk,
                battleHostEpoch));
        }
    }

    private long NextActionSequence(Guid agentId)
    {
        _localAgentStates.TryGetValue(agentId, out var state);
        state.Sequence++;
        _localAgentStates[agentId] = state;
        return state.Sequence;
    }

    public void ApplyRemoteGuardStates()
    {
        remoteActionProcessor.ApplyRemoteGuardStates();
    }

    public void RefreshRemoteGuardStatesAfterMovement()
    {
        remoteActionProcessor.RefreshRemoteGuardStatesAfterMovement();
    }

    public void ReplayRemoteGuardReactions()
    {
        guardReactionHandler.ProcessPendingReactions();
        remoteActionProcessor.ReplayRemoteGuardReactions();
    }

    public void ObserveBlockedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult)
    {
        if (isBlocked
            && affectedAgent != null
            && affectorAgent != null
            && agentRegistry.IsLocallyControlled(affectorAgent))
        {
            messageBroker.Publish(
                this,
                new LocalAgentGuardedHit(
                    affectedAgent,
                    affectorAgent));
        }

        guardReactionHandler.ObserveBlockedHit(
            affectedAgent,
            affectorAgent,
            isBlocked,
            isMissile,
            collisionResult,
            remoteActionProcessor.GetOutgoingBattleHostEpoch());
    }

    private void Handle_BattleHostAssigned(
        MessagePayload<NetworkBattleHostAssigned> payload)
    {
        if (_disposed) return;

        remoteActionProcessor.HandleBattleHostAssigned(payload.What);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (_disposed) return;

        remoteActionProcessor.Receive((AgentActionPacket)packet);
    }

    // Discrete actions worth replicating explicitly. Pure locomotion (Idle / the generic Other bucket that
    // walk/run fall into) is reproduced on the puppet from the continuous movement packet, so it is NOT sent.
    private static bool IsDiscreteAction(Agent.ActionCodeType type)
    {
        return type != Agent.ActionCodeType.Other && type != Agent.ActionCodeType.Idle;
    }

    private static bool IsMountedGuardLocomotionChurn(
        bool heldMountedGuardUnchanged,
        bool actionChanged,
        int action,
        bool actionDiscrete,
        bool actionDefending,
        bool previousActionDefending,
        bool hasDefendingAction,
        int defendingAction)
    {
        if (!heldMountedGuardUnchanged || !actionChanged)
            return false;

        if (!actionDiscrete)
            return previousActionDefending;

        return actionDefending
            && !previousActionDefending
            && hasDefendingAction
            && action == defendingAction;
    }

    private static int GetGuardReactionChannel(
        Agent agent,
        bool hadState,
        LocalAgentActionState state)
    {
        if (IsGuardReactionTransition(
                agent,
                channel: 1,
                hadState,
                state.Action1,
                state.Action1WasDefending))
        {
            return 1;
        }

        return IsGuardReactionTransition(
            agent,
            channel: 0,
            hadState,
            state.Action0,
            state.Action0WasDefending)
            ? 0
            : -1;
    }

    private static bool IsGuardReactionTransition(
        Agent agent,
        int channel,
        bool hadState,
        int previousAction,
        bool previousActionWasDefending)
    {
        Agent.ActionCodeType actionType =
            agent.GetCurrentActionType(channel);
        if (AgentActionData.IsGuardReactionAction(actionType))
            return true;

        int action = agent.GetCurrentAction(channel).Index;
        return hadState
            && previousActionWasDefending
            && action >= 0
            && action != previousAction
            && AgentActionData.IsDefendingAction(actionType)
            && agent.GetCurrentActionStage(channel)
                == Agent.ActionStage.DefendParry;
    }

    private static bool HasDefendStateChanged(
        Agent.MovementControlFlag previousFlags,
        Agent.MovementControlFlag currentFlags,
        Agent.GuardMode previousGuard,
        Agent.GuardMode currentGuard)
    {
        bool wasDefending =
            previousFlags != Agent.MovementControlFlag.None;
        bool isDefending =
            currentFlags != Agent.MovementControlFlag.None;
        if (wasDefending != isDefending)
            return true;

        if (!wasDefending
            || (AgentActionData.IsGuardMode(previousGuard)
                && AgentActionData.IsGuardMode(currentGuard)))
        {
            return false;
        }

        return previousFlags != currentFlags;
    }

    private Agent.GuardMode GetEffectiveGuardMode(
        Guid agentId,
        Agent agent,
        Agent.MovementControlFlag defendFlags,
        Agent.GuardMode actionGuardMode)
    {
        Agent.GuardMode guardMode = AgentActionData.GetEffectiveGuardMode(
            agent,
            defendFlags);
        if (agent.HasMount
            && AgentActionData.IsGuardMode(actionGuardMode)
            && _localAgentStates.TryGetValue(
                agentId,
                out LocalAgentActionState observedState)
            && observedState.HasObservation
            && AgentActionData.IsGuardMode(observedState.GuardMode))
        {
            Agent.GuardMode flagGuardMode =
                AgentActionData.GetGuardModeFromDefendFlags(defendFlags);
            if (AgentActionData.IsGuardMode(flagGuardMode)
                && flagGuardMode != actionGuardMode)
            {
                // Mounted input and native guard actions can update on different frames.
                // Follow the signal that changed, then retain it until the other catches up.
                Agent.GuardMode previousFlagGuardMode =
                    AgentActionData.GetGuardModeFromDefendFlags(
                        observedState.DefendFlags);
                bool actionDirectionChanged =
                    actionGuardMode != observedState.ActionGuardMode;
                bool flagDirectionChanged =
                    flagGuardMode != previousFlagGuardMode;
                if (actionDirectionChanged != flagDirectionChanged)
                {
                    return actionDirectionChanged
                        ? actionGuardMode
                        : flagGuardMode;
                }

                if (observedState.GuardMode == actionGuardMode
                    || observedState.GuardMode == flagGuardMode)
                {
                    return observedState.GuardMode;
                }
            }
        }

        if (agent.HasMount
            && defendFlags != Agent.MovementControlFlag.None
            && !AgentActionData.IsGuardMode(guardMode)
            && _localAgentStates.TryGetValue(
                agentId,
                out LocalAgentActionState state)
            && AgentActionData.IsGuardMode(state.GuardMode))
        {
            return state.GuardMode;
        }

        return guardMode;
    }

    private static void UpdateDefendingAction(
        int action,
        Agent.ActionCodeType actionType,
        ref bool hasDefendingAction,
        ref int defendingAction)
    {
        if (AgentActionData.IsDefendingAction(actionType))
        {
            hasDefendingAction = true;
            defendingAction = action;
        }
        else if (IsDiscreteAction(actionType))
        {
            // A strike or reaction ended the retained episode. The next guard action must be sent.
            hasDefendingAction = false;
        }
    }

    internal static bool IsOrderGesture(string actionName)
    {
        if (actionName == null) return false;

        return actionName == "act_command"
            || actionName.StartsWith("act_command_", StringComparison.Ordinal)
            || actionName == "act_horse_command"
            || actionName.StartsWith("act_horse_command_", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        messageBroker.Unsubscribe<NetworkBattleHostAssigned>(Handle_BattleHostAssigned);
        packetManager.RemovePacketHandler(this);
        remoteActionProcessor.Dispose();
        guardReactionHandler.Dispose();
        _localAgentStates.Clear();
    }
}
