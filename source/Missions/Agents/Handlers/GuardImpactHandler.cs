using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Battles;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IGuardImpactHandler : IDisposable
{
    void ObserveBlockedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult,
        int priorGuardChannel,
        ActionIndexCache priorGuardAction,
        int battleHostEpoch);

    void CapturePendingLocalImpacts();
    void ReplayRemoteImpacts(float dt);
#if DEBUG
    bool TryGetGuardImpact(
        Guid agentId,
        out int channel,
        out int guardActionIndex,
        out int animationIndex,
        out float progress);
#endif
}

public class GuardImpactHandler : IGuardImpactHandler
{
    private const int MaximumCaptureAttempts = 2;
    private const float MaximumProgress = 0.999f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry battleHostRegistry;
    private readonly IAgentVisualActionAccessor visualActionAccessor;
    private readonly Dictionary<Guid, PendingGuardImpact> pendingImpacts =
        new Dictionary<Guid, PendingGuardImpact>();
    private readonly Dictionary<Guid, RemoteGuardImpact> remoteImpacts =
        new Dictionary<Guid, RemoteGuardImpact>();
    private readonly Dictionary<(string ControllerId, int BattleHostEpoch), long>
        receivedSequences =
            new Dictionary<(string ControllerId, int BattleHostEpoch), long>();
#if DEBUG
    private readonly Dictionary<Guid, RemoteGuardImpact> localImpactEvidence =
        new Dictionary<Guid, RemoteGuardImpact>();
#endif

    private long sequence;
    private bool disposed;

    private struct PendingGuardImpact
    {
        public readonly Guid AgentId;
        public readonly Guid AttackerAgentId;
        public readonly Agent Agent;
        public readonly int Channel;
        public readonly ActionIndexCache GuardAction;
        public readonly int GuardAnimationIndex;
        public readonly int BattleHostEpoch;
        public int CaptureAttempts;

        public PendingGuardImpact(
            Guid agentId,
            Guid attackerAgentId,
            Agent agent,
            int channel,
            ActionIndexCache guardAction,
            int guardAnimationIndex,
            int battleHostEpoch)
        {
            AgentId = agentId;
            AttackerAgentId = attackerAgentId;
            Agent = agent;
            Channel = channel;
            GuardAction = guardAction;
            GuardAnimationIndex = guardAnimationIndex;
            BattleHostEpoch = battleHostEpoch;
            CaptureAttempts = 0;
        }
    }

    private struct RemoteGuardImpact
    {
        public readonly NetworkAgentGuardImpact Message;
        public float Progress;

        public RemoteGuardImpact(NetworkAgentGuardImpact message)
        {
            Message = message;
            Progress = message.Progress;
        }
    }

    public GuardImpactHandler(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IBattleHostRegistry battleHostRegistry,
        IAgentVisualActionAccessor visualActionAccessor)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.battleHostRegistry = battleHostRegistry;
        this.visualActionAccessor = visualActionAccessor;

        messageBroker.Subscribe<NetworkAgentGuardImpact>(
            Handle_NetworkAgentGuardImpact);
    }

    public void ObserveBlockedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult,
        int priorGuardChannel,
        ActionIndexCache priorGuardAction,
        int battleHostEpoch)
    {
        if (disposed
            || Mission.Current == null
            || !isBlocked
            || isMissile
            || !IsBlockedCollision(collisionResult)
            || affectedAgent == null
            || affectorAgent == null
            || !agentRegistry.IsLocallyControlled(affectorAgent)
            || !agentRegistry.TryGetAgentInfo(
                affectedAgent,
                out CoopAgentInfo affectedInfo)
            || !agentRegistry.TryGetAgentInfo(
                affectorAgent,
                out CoopAgentInfo affectorInfo))
        {
            return;
        }

        int channel = priorGuardChannel;
        ActionIndexCache guardAction = priorGuardAction;
        if ((channel < 0 || channel > 1)
            || guardAction == ActionIndexCache.act_none)
        {
            channel = GetGuardActionChannel(affectedAgent);
            if (channel < 0) return;
            guardAction = affectedAgent.GetCurrentAction(channel);
        }
        if (guardAction == ActionIndexCache.act_none) return;

        int guardAnimationIndex = visualActionAccessor.GetAnimationIndex(
            affectedAgent,
            in guardAction);
        if (guardAnimationIndex < 0) return;

        pendingImpacts[affectedInfo.AgentId] = new PendingGuardImpact(
            affectedInfo.AgentId,
            affectorInfo.AgentId,
            affectedAgent,
            channel,
            guardAction,
            guardAnimationIndex,
            battleHostEpoch);
    }

    public void CapturePendingLocalImpacts()
    {
        if (disposed || Mission.Current == null) return;

        CapturePendingImpacts();
    }

    private void CapturePendingImpacts()
    {
        if (pendingImpacts.Count == 0) return;

        var completedIds = new List<Guid>();
        var pendingIds = new List<Guid>(pendingImpacts.Keys);
        foreach (Guid agentId in pendingIds)
        {
            PendingGuardImpact pending = pendingImpacts[agentId];
            Agent agent = pending.Agent;
            if (agent == null
                || agent.Mission != Mission.Current
                || !agent.IsActive())
            {
                completedIds.Add(agentId);
                continue;
            }

            if (!visualActionAccessor.TryGetAnimationState(
                    agent,
                    pending.Channel,
                    out int animationIndex,
                    out float progress,
                    out float speed)
                || animationIndex == pending.GuardAnimationIndex)
            {
                pending.CaptureAttempts++;
                if (pending.CaptureAttempts >= MaximumCaptureAttempts)
                    completedIds.Add(agentId);
                else
                    pendingImpacts[agentId] = pending;
                continue;
            }

            float duration =
                visualActionAccessor.GetAnimationDuration(animationIndex);
            if (duration <= 0f
                || float.IsNaN(duration)
                || float.IsInfinity(duration))
            {
                completedIds.Add(agentId);
                continue;
            }

            progress = ClampProgress(progress);
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                speed = 1f;

            sequence++;
            var message = new NetworkAgentGuardImpact(
                controllerIdProvider.ControllerId,
                sequence,
                pending.BattleHostEpoch,
                pending.AttackerAgentId,
                pending.AgentId,
                pending.Channel,
                pending.GuardAction.Index,
                animationIndex,
                progress,
                speed,
                duration);
            network.SendAll(message);
#if DEBUG
            localImpactEvidence[agentId] = new RemoteGuardImpact(message);
#endif
            completedIds.Add(agentId);
        }

        foreach (Guid agentId in completedIds)
            pendingImpacts.Remove(agentId);
    }

    public void ReplayRemoteImpacts(float dt)
    {
        if (disposed || Mission.Current == null)
            return;

#if DEBUG
        AdvanceLocalImpactEvidence(dt);
#endif
        if (remoteImpacts.Count == 0) return;

        List<Guid> completedIds = null;
        var impactIds = new List<Guid>(remoteImpacts.Keys);
        foreach (Guid agentId in impactIds)
        {
            if (!agentRegistry.TryGetAgentInfo(
                    agentId,
                    out CoopAgentInfo agentInfo)
                || agentInfo.Agent == null
                || agentInfo.Agent.Mission != Mission.Current
                || !agentInfo.Agent.IsActive())
            {
                (completedIds ??= new List<Guid>()).Add(agentId);
                continue;
            }

            RemoteGuardImpact impact = remoteImpacts[agentId];
            NetworkAgentGuardImpact message = impact.Message;
            if (HasInterruptingAction(agentInfo.Agent)
                || !HasLogicalGuard(agentInfo.Agent))
            {
                (completedIds ??= new List<Guid>()).Add(agentId);
                continue;
            }

            var guardAction = new ActionIndexCache(
                message.GuardActionIndex);
            int guardAnimationIndex =
                visualActionAccessor.GetAnimationIndex(
                    agentInfo.Agent,
                    in guardAction);
            if (guardAnimationIndex < 0
                || !visualActionAccessor.TryGetAnimationState(
                    agentInfo.Agent,
                    message.Channel,
                    out int visibleAnimationIndex,
                    out _,
                    out _)
                || (visibleAnimationIndex != guardAnimationIndex
                    && visibleAnimationIndex != message.AnimationIndex))
            {
                (completedIds ??= new List<Guid>()).Add(agentId);
                continue;
            }

            if (dt > 0f)
            {
                impact.Progress +=
                    (dt * message.Speed) / message.Duration;
            }

            bool completed = impact.Progress >= MaximumProgress;
            impact.Progress = Math.Min(impact.Progress, MaximumProgress);
            visualActionAccessor.AdvanceAnimationIfAvailable(
                agentInfo.Agent,
                message.Channel,
                message.AnimationIndex,
                impact.Progress,
                message.Speed);

            if (completed)
                (completedIds ??= new List<Guid>()).Add(agentId);
            else
                remoteImpacts[agentId] = impact;
        }

        if (completedIds == null) return;
        foreach (Guid agentId in completedIds)
            remoteImpacts.Remove(agentId);
    }

#if DEBUG
    public bool TryGetGuardImpact(
        Guid agentId,
        out int channel,
        out int guardActionIndex,
        out int animationIndex,
        out float progress)
    {
        channel = -1;
        guardActionIndex = -1;
        animationIndex = -1;
        progress = -1f;
        if (!remoteImpacts.TryGetValue(
                agentId,
                out RemoteGuardImpact impact)
            && !localImpactEvidence.TryGetValue(agentId, out impact))
        {
            return false;
        }

        channel = impact.Message.Channel;
        guardActionIndex = impact.Message.GuardActionIndex;
        animationIndex = impact.Message.AnimationIndex;
        progress = impact.Progress;
        return true;
    }

    private void AdvanceLocalImpactEvidence(float dt)
    {
        if (localImpactEvidence.Count == 0 || dt <= 0f) return;

        List<Guid> completedIds = null;
        var agentIds = new List<Guid>(localImpactEvidence.Keys);
        foreach (Guid agentId in agentIds)
        {
            RemoteGuardImpact impact = localImpactEvidence[agentId];
            impact.Progress +=
                (dt * impact.Message.Speed) / impact.Message.Duration;
            if (impact.Progress >= MaximumProgress)
                (completedIds ??= new List<Guid>()).Add(agentId);
            else
                localImpactEvidence[agentId] = impact;
        }

        if (completedIds == null) return;
        foreach (Guid agentId in completedIds)
            localImpactEvidence.Remove(agentId);
    }
#endif

    private void Handle_NetworkAgentGuardImpact(
        MessagePayload<NetworkAgentGuardImpact> payload)
    {
        NetworkAgentGuardImpact message = payload.What;
        if (disposed
            || string.IsNullOrEmpty(message.SourceControllerId)
            || message.Sequence <= 0
            || message.BattleHostEpoch < 0
            || message.AttackerAgentId == Guid.Empty
            || message.AgentId == Guid.Empty
            || message.Channel < 0
            || message.Channel > 1
            || message.AnimationIndex < 0
            || message.GuardActionIndex < 0
            || message.Progress < 0f
            || message.Progress > MaximumProgress
            || float.IsNaN(message.Progress)
            || float.IsInfinity(message.Progress)
            || message.Duration <= 0f
            || float.IsNaN(message.Duration)
            || float.IsInfinity(message.Duration)
            || message.Speed <= 0f
            || float.IsNaN(message.Speed)
            || float.IsInfinity(message.Speed))
        {
            return;
        }

        GameThread.RunSafe(
            () => ReceiveOnGameThread(message),
            context: nameof(Handle_NetworkAgentGuardImpact));
    }

    private void ReceiveOnGameThread(NetworkAgentGuardImpact message)
    {
        if (disposed || Mission.Current == null) return;
        var sequenceKey = (
            message.SourceControllerId,
            message.BattleHostEpoch);
        if (receivedSequences.TryGetValue(
                sequenceKey,
                out long receivedSequence)
            && receivedSequence >= message.Sequence)
        {
            return;
        }

        if (!agentRegistry.TryGetAgentInfo(
                message.AttackerAgentId,
                out CoopAgentInfo attackerInfo)
            || !IsCurrentAttackerAuthority(attackerInfo, message)
            || agentRegistry.IsLocallyControlled(
                message.AttackerAgentId)
            || !agentRegistry.TryGetAgentInfo(message.AgentId, out _))
            return;

        receivedSequences[sequenceKey] = message.Sequence;
        remoteImpacts[message.AgentId] = new RemoteGuardImpact(message);
    }

    private bool IsCurrentAttackerAuthority(
        CoopAgentInfo attackerInfo,
        NetworkAgentGuardImpact message)
    {
        if (message.BattleHostEpoch == 0)
        {
            return attackerInfo.CurrentAuthority
                == message.SourceControllerId;
        }

        string mapEventId = BattleSpawnGate.ActiveMapEventId;
        return mapEventId != null
            && battleHostRegistry.TryGet(
                mapEventId,
                out BattleHostAssignment assignment)
            && assignment.HostControllerId
                == message.SourceControllerId
            && assignment.Epoch == message.BattleHostEpoch;
    }

    private static int GetGuardActionChannel(Agent agent)
    {
        if (AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(1)))
        {
            return 1;
        }

        return AgentActionData.IsDefendingAction(
            agent.GetCurrentActionType(0))
            ? 0
            : -1;
    }

    private static bool IsBlockedCollision(
        CombatCollisionResult collisionResult)
    {
        return collisionResult == CombatCollisionResult.Blocked
            || collisionResult == CombatCollisionResult.Parried
            || collisionResult == CombatCollisionResult.ChamberBlocked;
    }

    private static bool HasLogicalGuard(Agent agent)
    {
        return AgentActionData.IsGuardMode(agent.CurrentGuardMode)
            || AgentActionData.GetEffectiveDefendMovementFlags(agent)
                != Agent.MovementControlFlag.None
            || AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(0))
            || AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(1));
    }

    private static bool HasInterruptingAction(Agent agent)
    {
        return IsInterruptingAction(agent.GetCurrentActionType(0))
            || IsInterruptingAction(agent.GetCurrentActionType(1));
    }

    private static bool IsInterruptingAction(
        Agent.ActionCodeType actionType)
    {
        return actionType != Agent.ActionCodeType.Other
            && actionType != Agent.ActionCodeType.Idle
            && !AgentActionData.IsDefendingAction(actionType);
    }

    private static float ClampProgress(float progress)
    {
        if (float.IsNaN(progress) || float.IsInfinity(progress))
            return 0f;
        return Math.Max(0f, Math.Min(progress, MaximumProgress));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        messageBroker.Unsubscribe<NetworkAgentGuardImpact>(
            Handle_NetworkAgentGuardImpact);
        pendingImpacts.Clear();
        remoteImpacts.Clear();
        receivedSequences.Clear();
#if DEBUG
        localImpactEvidence.Clear();
#endif
    }
}
