using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Battles;
#if DEBUG
using Missions.Diagnostics;
#endif
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IGuardReactionHandler : IDisposable
{
    void ObserveBlockedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult,
        int battleHostEpoch);

    void ProcessPendingReactions();
}

public class GuardReactionHandler : IGuardReactionHandler
{
    private const int MaximumCaptureAttempts = 3;
    private const int MaximumRemoteApplyAttempts = 6;
    private const float MaximumProgress = 0.999f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry battleHostRegistry;
    private readonly IGuardReactionActionResolver reactionActionResolver;
    private readonly Dictionary<Guid, PendingGuardReaction> pendingReactions =
        new Dictionary<Guid, PendingGuardReaction>();
    private readonly Dictionary<Guid, PendingRemoteGuardReaction>
        pendingRemoteReactions =
            new Dictionary<Guid, PendingRemoteGuardReaction>();
    private readonly Dictionary<(string ControllerId, int BattleHostEpoch), long>
        receivedSequences =
            new Dictionary<(string ControllerId, int BattleHostEpoch), long>();
    private readonly Dictionary<Guid, (
        string ControllerId,
        int BattleHostEpoch,
        long Sequence)> appliedReactions =
            new Dictionary<Guid, (
                string ControllerId,
                int BattleHostEpoch,
                long Sequence)>();

    private long sequence;
    private bool disposed;

    private struct PendingGuardReaction
    {
        public readonly Guid AgentId;
        public readonly Guid AttackerAgentId;
        public readonly Agent Agent;
        public readonly int BattleHostEpoch;
        public readonly bool DefenderLocallyControlled;
        public int CaptureAttempts;

        public PendingGuardReaction(
            Guid agentId,
            Guid attackerAgentId,
            Agent agent,
            int battleHostEpoch,
            bool defenderLocallyControlled)
        {
            AgentId = agentId;
            AttackerAgentId = attackerAgentId;
            Agent = agent;
            BattleHostEpoch = battleHostEpoch;
            DefenderLocallyControlled = defenderLocallyControlled;
            CaptureAttempts = 0;
        }
    }

    private struct PendingRemoteGuardReaction
    {
        public readonly NetworkAgentGuardReaction Message;
        public int ApplyAttempts;

        public PendingRemoteGuardReaction(
            NetworkAgentGuardReaction message)
        {
            Message = message;
            ApplyAttempts = 0;
        }
    }

    private enum RemoteReactionApplyResult
    {
        Applied,
        Retry,
        Invalid
    }

    private enum AuthorityValidationResult
    {
        Valid,
        Retry,
        Invalid
    }

    public GuardReactionHandler(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IBattleHostRegistry battleHostRegistry,
        IGuardReactionActionResolver reactionActionResolver)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.battleHostRegistry = battleHostRegistry;
        this.reactionActionResolver = reactionActionResolver;

        messageBroker.Subscribe<NetworkAgentGuardReaction>(
            Handle_NetworkAgentGuardReaction);
    }

    public void ObserveBlockedHit(
        Agent affectedAgent,
        Agent affectorAgent,
        bool isBlocked,
        bool isMissile,
        CombatCollisionResult collisionResult,
        int battleHostEpoch)
    {
        if (disposed
            || Mission.Current == null
            || !isBlocked
            || isMissile
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

        pendingReactions[affectedInfo.AgentId] =
            new PendingGuardReaction(
                affectedInfo.AgentId,
                affectorInfo.AgentId,
                affectedAgent,
                battleHostEpoch,
                agentRegistry.IsLocallyControlled(
                    affectedAgent));
    }

    public void ProcessPendingReactions()
    {
        if (disposed
            || Mission.Current == null)
        {
            return;
        }

        if (pendingReactions.Count > 0)
        {
            var completedIds = new List<Guid>();
            var pendingIds = new List<Guid>(pendingReactions.Keys);
            foreach (Guid agentId in pendingIds)
            {
                PendingGuardReaction pending = pendingReactions[agentId];
                Agent agent = pending.Agent;
                if (agent == null
                    || agent.Mission != Mission.Current
                    || !agent.IsActive())
                {
                    completedIds.Add(agentId);
                    continue;
                }

                if (!TryGetGuardReaction(
                        agent,
                        out int reactionChannel,
                        out ActionIndexCache reactionAction))
                {
                    if (!pending.DefenderLocallyControlled
                        && reactionActionResolver.TryResolve(
                            agent,
                            out reactionChannel,
                            out reactionAction,
                            out AnimFlags reactionFlags)
                        && TryApplySyntheticReaction(
                            agent,
                            reactionChannel,
                            in reactionAction,
                            reactionFlags))
                    {
                        SendReaction(
                            pending,
                            agent,
                            reactionChannel,
                            in reactionAction,
                            progress: 0f,
                            reactionFlags);
                        completedIds.Add(agentId);
                        continue;
                    }

                    pending.CaptureAttempts++;
                    if (pending.CaptureAttempts >= MaximumCaptureAttempts)
                    {
                        completedIds.Add(agentId);
                    }
                    else
                    {
                        pendingReactions[agentId] = pending;
                    }
                    continue;
                }

                float progress = ClampProgress(
                    agent.GetCurrentActionProgress(reactionChannel));
                SendReaction(
                    pending,
                    agent,
                    reactionChannel,
                    in reactionAction,
                    progress,
                    agent.GetCurrentAnimationFlag(
                        reactionChannel));
                completedIds.Add(agentId);
            }

            foreach (Guid agentId in completedIds)
                pendingReactions.Remove(agentId);
        }

        ApplyPendingRemoteReactions();
    }

    private bool TryApplySyntheticReaction(
        Agent agent,
        int channel,
        in ActionIndexCache reactionAction,
        AnimFlags animationFlags)
    {
        if (HasInterruptingAction(agent))
            return false;

        using (new AllowedThread())
        {
#if DEBUG
            if (MissionActionDiagnostics.AnimationTraceEnabled)
            {
                Guid diagnosticAgentId =
                    agentRegistry.TryGetAgentInfo(
                        agent,
                        out CoopAgentInfo diagnosticInfo)
                        ? diagnosticInfo.AgentId
                        : Guid.Empty;
                MissionActionDiagnostics.RecordExternalActionCommand(
                    diagnosticAgentId,
                    agent,
                    channel,
                    reactionAction.Index,
                    startProgress: 0f,
                    animationFlags,
                    "synthetic-guard-reaction");
            }
#endif
            return agent.SetActionChannel(
                channel,
                in reactionAction,
                additionalFlags: animationFlags,
                startProgress: 0f);
        }
    }

    private void SendReaction(
        PendingGuardReaction pending,
        Agent agent,
        int reactionChannel,
        in ActionIndexCache reactionAction,
        float progress,
        AnimFlags animationFlags)
    {
        sequence++;
        network.SendAll(
            new NetworkAgentGuardReaction(
                controllerIdProvider.ControllerId,
                sequence,
                pending.BattleHostEpoch,
                pending.AttackerAgentId,
                pending.AgentId,
                reactionChannel,
                reactionAction.Index,
                progress,
                (ulong)animationFlags,
                agent.HasMount));
    }

    private void Handle_NetworkAgentGuardReaction(
        MessagePayload<NetworkAgentGuardReaction> payload)
    {
        NetworkAgentGuardReaction message = payload.What;
        if (disposed
            || string.IsNullOrEmpty(message.SourceControllerId)
            || message.Sequence <= 0
            || message.BattleHostEpoch < 0
            || message.AttackerAgentId == Guid.Empty
            || message.AgentId == Guid.Empty
            || message.AttackerAgentId == message.AgentId
            || message.ReactionChannel < 0
            || message.ReactionChannel > 1
            || message.ReactionActionIndex < 0
            || message.Progress < 0f
            || message.Progress > MaximumProgress
            || float.IsNaN(message.Progress)
            || float.IsInfinity(message.Progress))
        {
            return;
        }

        GameThread.RunSafe(
            () => ReceiveOnGameThread(message),
            context: nameof(Handle_NetworkAgentGuardReaction));
    }

    private void ReceiveOnGameThread(NetworkAgentGuardReaction message)
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

        receivedSequences[sequenceKey] = message.Sequence;
        var pending = new PendingRemoteGuardReaction(message);
        if (TryApplyRemoteReaction(message)
            == RemoteReactionApplyResult.Retry)
        {
            pendingRemoteReactions[message.AgentId] = pending;
        }
        else
        {
            pendingRemoteReactions.Remove(message.AgentId);
        }
    }

    private void ApplyPendingRemoteReactions()
    {
        if (pendingRemoteReactions.Count == 0) return;

        var completedIds = new List<Guid>();
        var pendingIds =
            new List<Guid>(pendingRemoteReactions.Keys);
        foreach (Guid agentId in pendingIds)
        {
            PendingRemoteGuardReaction pending =
                pendingRemoteReactions[agentId];
            RemoteReactionApplyResult result =
                TryApplyRemoteReaction(pending.Message);
            if (result != RemoteReactionApplyResult.Retry)
            {
                completedIds.Add(agentId);
                continue;
            }

            pending.ApplyAttempts++;
            if (pending.ApplyAttempts >= MaximumRemoteApplyAttempts)
                completedIds.Add(agentId);
            else
                pendingRemoteReactions[agentId] = pending;
        }

        foreach (Guid agentId in completedIds)
            pendingRemoteReactions.Remove(agentId);
    }

    private RemoteReactionApplyResult TryApplyRemoteReaction(
        NetworkAgentGuardReaction message)
    {
        AuthorityValidationResult authorityResult =
            ValidateSourceAuthority(message);
        if (authorityResult != AuthorityValidationResult.Valid)
        {
            return authorityResult == AuthorityValidationResult.Retry
                ? RemoteReactionApplyResult.Retry
                : RemoteReactionApplyResult.Invalid;
        }

        if (!agentRegistry.TryGetAgentInfo(
                message.AgentId,
                out CoopAgentInfo agentInfo))
        {
            return RemoteReactionApplyResult.Retry;
        }

        Agent agent = agentInfo.Agent;
        if (agent == null
            || agent.Mission != Mission.Current)
        {
            return RemoteReactionApplyResult.Retry;
        }
        if (!agent.IsActive() || agent.Health <= 0f)
        {
            return RemoteReactionApplyResult.Invalid;
        }
        if (agent.HasMount != message.IsMounted)
        {
            return RemoteReactionApplyResult.Retry;
        }

        var reactionAction =
            new ActionIndexCache(message.ReactionActionIndex);
        if (agent.GetCurrentAction(message.ReactionChannel)
                == reactionAction
            && !ShouldRestartMatchingReaction(message))
        {
            RecordAppliedReaction(message);
            return RemoteReactionApplyResult.Applied;
        }

        if (HasInterruptingAction(agent))
        {
            return RemoteReactionApplyResult.Invalid;
        }
        bool applied;
        using (new AllowedThread())
        {
#if DEBUG
            MissionActionDiagnostics.RecordExternalActionCommand(
                message.AgentId,
                agent,
                message.ReactionChannel,
                reactionAction.Index,
                message.Progress,
                (AnimFlags)message.AnimationFlags,
                "remote-guard-reaction");
#endif
            applied = agent.SetActionChannel(
                message.ReactionChannel,
                in reactionAction,
                additionalFlags:
                    (AnimFlags)message.AnimationFlags,
                startProgress: message.Progress);
        }
        if (applied)
            RecordAppliedReaction(message);
        return applied
            ? RemoteReactionApplyResult.Applied
            : RemoteReactionApplyResult.Retry;
    }

    private AuthorityValidationResult ValidateSourceAuthority(
        NetworkAgentGuardReaction message)
    {
        if (message.SourceControllerId
            == controllerIdProvider.ControllerId)
        {
            return AuthorityValidationResult.Invalid;
        }

        if (message.BattleHostEpoch == 0)
        {
            if (!agentRegistry.TryGetAgentInfo(
                    message.AttackerAgentId,
                    out CoopAgentInfo attackerInfo))
            {
                return AuthorityValidationResult.Retry;
            }

            return attackerInfo.CurrentAuthority
                    == message.SourceControllerId
                ? AuthorityValidationResult.Valid
                : AuthorityValidationResult.Invalid;
        }

        string mapEventId = BattleSpawnGate.ActiveMapEventId;
        if (mapEventId == null
            || !battleHostRegistry.TryGet(
                mapEventId,
                out BattleHostAssignment assignment)
            || assignment.Epoch < message.BattleHostEpoch)
        {
            return AuthorityValidationResult.Retry;
        }

        return assignment.Epoch == message.BattleHostEpoch
                && assignment.HostControllerId
                    == message.SourceControllerId
            ? AuthorityValidationResult.Valid
            : AuthorityValidationResult.Invalid;
    }

    private bool ShouldRestartMatchingReaction(
        NetworkAgentGuardReaction message)
    {
        return appliedReactions.TryGetValue(
                message.AgentId,
                out var applied)
            && (applied.ControllerId
                    != message.SourceControllerId
                || applied.BattleHostEpoch
                    != message.BattleHostEpoch
                || applied.Sequence < message.Sequence);
    }

    private void RecordAppliedReaction(
        NetworkAgentGuardReaction message)
    {
        appliedReactions[message.AgentId] = (
            message.SourceControllerId,
            message.BattleHostEpoch,
            message.Sequence);
    }

    private static bool TryGetGuardReaction(
        Agent agent,
        out int reactionChannel,
        out ActionIndexCache reactionAction)
    {
        if (IsGuardReaction(agent, channel: 1))
        {
            reactionChannel = 1;
            reactionAction =
                agent.GetCurrentAction(reactionChannel);
            return true;
        }

        if (IsGuardReaction(agent, channel: 0))
        {
            reactionChannel = 0;
            reactionAction =
                agent.GetCurrentAction(reactionChannel);
            return true;
        }

        reactionChannel = -1;
        reactionAction = ActionIndexCache.act_none;
        return false;
    }

    private static bool IsGuardReaction(
        Agent agent,
        int channel)
    {
        ActionIndexCache action =
            agent.GetCurrentAction(channel);
        if (action == ActionIndexCache.act_none)
            return false;

        Agent.ActionCodeType actionType =
            agent.GetCurrentActionType(channel);
        if (AgentActionData.IsGuardReactionAction(actionType))
            return true;

        return AgentActionData.IsDefendingAction(actionType)
            && agent.GetCurrentActionStage(channel)
                == Agent.ActionStage.DefendParry;
    }

    private static bool HasInterruptingAction(Agent agent)
    {
        return IsInterruptingAction(
                agent.GetCurrentActionType(0))
            || IsInterruptingAction(
                agent.GetCurrentActionType(1));
    }

    private static bool IsInterruptingAction(
        Agent.ActionCodeType actionType)
    {
        return actionType != Agent.ActionCodeType.Other
            && actionType != Agent.ActionCodeType.Idle
            && !AgentActionData.IsDefendingAction(actionType)
            && !AgentActionData.IsGuardReactionAction(actionType);
    }

    private static float ClampProgress(float progress)
    {
        if (float.IsNaN(progress)
            || float.IsInfinity(progress))
        {
            return 0f;
        }

        return Math.Max(
            0f,
            Math.Min(progress, MaximumProgress));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        messageBroker.Unsubscribe<NetworkAgentGuardReaction>(
            Handle_NetworkAgentGuardReaction);
        pendingReactions.Clear();
        pendingRemoteReactions.Clear();
        receivedSequences.Clear();
        appliedReactions.Clear();
    }
}
