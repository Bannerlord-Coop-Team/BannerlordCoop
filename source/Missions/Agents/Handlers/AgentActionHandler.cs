using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Agents.Packets;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentActionHandler : IPacketHandler, IDisposable
{
    /// <summary>
    /// [Game thread] Detect discrete action, defend-input, and realized guard changes on owned agents and broadcast them. Driven per-frame from
    /// CoopMissionController.OnMissionTick: the game thread is the only place a
    /// one-frame action transition can be observed without racing the engine, and event capture must be exact.
    /// </summary>
    void PollActions();

    /// <summary>[Any thread] Send held defend and guard state for owned agents to a joining peer.</summary>
    void CatchUpJoiner(string controllerId);

    /// <summary>[Game thread] Reassert received puppet defend state from the mission pre-tick hook.</summary>
    void ReassertRemoteDefendStates();

    /// <summary>[Game thread] Apply queued remote actions and refresh their retained defend state.</summary>
    void ApplyRemoteGuardStates();
}

/// <summary>
/// Event-driven animation sync. Where movement is continuous state (polled, smoothed, unreliable), an ACTION is
/// an event — "this agent started a punch/attack/jump" — that plays out locally over time. So instead of polling
/// the full action state every tick and re-applying it (which lost one-frame triggers, fought the local
/// animation, and churned the skeleton), this diffs each owned agent's action channels ON THE GAME THREAD and,
/// only when a DISCRETE action, held defend input, or realized guard state changes, broadcasts it
/// <see cref="DeliveryMethod.ReliableOrdered"/>. The receiver applies the transition and lets the engine advance
/// it. Locomotion (walk/run/idle) is skipped — it is reproduced from the synced <c>MovementInputVector</c>.
/// </summary>
public class AgentActionHandler : IAgentActionHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentActionHandler>();

    // Reliable delivery fragments, so this is only to avoid one-giant-packet; action changes per frame are few.
    private const int MaxAgentsPerActionPacket = 8;

    private readonly IBattleNetwork client;
    private readonly IPacketManager packetManager;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IRemoteAgentActionProcessor remoteActionProcessor;

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
        public bool WasDiscrete;
        public long Sequence;
    }

    public AgentActionHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IRemoteAgentActionProcessor remoteActionProcessor)
    {
        this.client = client;
        this.packetManager = packetManager;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.remoteActionProcessor = remoteActionProcessor;

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
            Agent.GuardMode guardMode = AgentActionData.GetEffectiveGuardMode(
                agent,
                defendFlags);

            _localAgentStates.TryGetValue(info.AgentId, out var state);
            bool hadState = state.HasObservation;
            bool defendChanged;
            if (hadState)
            {
                defendChanged = state.DefendFlags != defendFlags;
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

            bool wasGuarding = hadState
                && (state.DefendFlags != Agent.MovementControlFlag.None
                    || AgentActionData.IsGuardMode(state.GuardMode));
            bool isGuarding = defendFlags != Agent.MovementControlFlag.None
                || AgentActionData.IsGuardMode(guardMode);

            if (hadState && state.Action0 == action0 && state.Action1 == action1
                && !defendChanged && !guardChanged)
                continue;

            bool nowDiscrete = IsDiscreteAction(agent.GetCurrentActionType(0))
                            || IsDiscreteAction(agent.GetCurrentActionType(1));

            // Native command actions are untyped, so recognize the main agent's order gesture by action name.
            if (!nowDiscrete && agent == Mission.Current.MainAgent)
            {
                nowDiscrete = IsOrderGesture(AgentActionData.GetActionNameWithCode(action0))
                           || IsOrderGesture(AgentActionData.GetActionNameWithCode(action1));
            }

            // Defend input and realized guard state can change before the animation index, so send them explicitly too.
            bool broadcast = defendChanged || guardChanged || nowDiscrete || (hadState && state.WasDiscrete);

            state.HasObservation = true;
            state.Action0 = action0;
            state.Action1 = action1;
            state.DefendFlags = defendFlags;
            state.GuardMode = guardMode;
            state.WasDiscrete = nowDiscrete;
            _localAgentStates[info.AgentId] = state;
            if (!broadcast)
                continue;

            long sequence = NextActionSequence(info.AgentId);
            (ids ??= new List<Guid>()).Add(info.AgentId);
            (actions ??= new List<AgentActionData>()).Add(
                new AgentActionData(agent, defendFlags, guardMode));
            (sequences ??= new List<long>()).Add(sequence);

            if (defendChanged || guardChanged)
            {
                try
                {
                    if (!(wasGuarding || isGuarding)
                        || agent != Mission.Current?.MainAgent
                        || !agent.HasMount)
                    {
                        continue;
                    }

                    var snapshot = new
                    {
                        RawDefend = AgentActionData.GetDefendMovementFlags(agent.MovementFlags),
                        NativeDefend = AgentActionData.GetDefendMovementFlags(agent.GetDefendMovementFlag()),
                        EffectiveDefend = defendFlags,
                        Guard = guardMode,
                        CurrentGuard = agent.CurrentGuardMode,
                        ControllerType = agent.Controller,
                        MountIndex = agent.MountAgent?.Index ?? -1,
                        MainHand = agent.GetPrimaryWieldedItemIndex(),
                        OffHand = agent.GetOffhandWieldedItemIndex(),
                        Action0 = new
                        {
                            Index = action0,
                            Type = agent.GetCurrentActionType(0),
                            Stage = agent.GetCurrentActionStage(0),
                            Direction = agent.GetCurrentActionDirection(0),
                            Progress = agent.GetCurrentActionProgress(0)
                        },
                        Action1 = new
                        {
                            Index = action1,
                            Type = agent.GetCurrentActionType(1),
                            Stage = agent.GetCurrentActionStage(1),
                            Direction = agent.GetCurrentActionDirection(1),
                            Progress = agent.GetCurrentActionProgress(1)
                        }
                    };
                    Logger.Debug(
                        "[AgentActionTrace] SEND controller {ControllerId}, agent {AgentId}, sequence {Sequence}: {@Snapshot}",
                        controllerIdProvider.ControllerId,
                        info.AgentId,
                        sequence,
                        snapshot);
                }
                catch (Exception exception)
                {
                    Logger.Debug(exception, "[AgentActionTrace] SEND snapshot failed for {AgentId}", info.AgentId);
                }
            }
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
                Agent.GuardMode guardMode = AgentActionData.GetEffectiveGuardMode(
                    agent,
                    defendFlags);
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

    public void ReassertRemoteDefendStates()
    {
        remoteActionProcessor.ReassertRemoteDefendStates();
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
    // walk/run fall into) is reproduced on the puppet from the synced MovementInputVector, so it is NOT sent.
    private static bool IsDiscreteAction(Agent.ActionCodeType type)
    {
        return type != Agent.ActionCodeType.Other && type != Agent.ActionCodeType.Idle;
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
        _localAgentStates.Clear();
    }
}
