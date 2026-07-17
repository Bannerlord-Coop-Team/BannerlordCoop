using Common;
using Common.Logging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Agents.Packets;
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

    /// <summary>[Game thread] Reassert received puppet guards once per mission frame.</summary>
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
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    // Last observed action indices, defend input, and guard state per owned agent, so we broadcast only on change. WasDiscrete
    // lets us also send the END of a discrete action while still skipping locomotion<->locomotion churn.
    private readonly Dictionary<Guid, ActionState> _lastActions = new Dictionary<Guid, ActionState>();
    private readonly Dictionary<Guid, Agent.GuardMode> _remoteGuardStates =
        new Dictionary<Guid, Agent.GuardMode>();

    private bool _disposed;

    private readonly struct ActionState
    {
        public readonly int Action0;
        public readonly int Action1;
        public readonly Agent.MovementControlFlag DefendFlags;
        public readonly Agent.GuardMode GuardMode;
        public readonly bool WasDiscrete;

        public ActionState(
            int action0,
            int action1,
            Agent.MovementControlFlag defendFlags,
            Agent.GuardMode guardMode,
            bool wasDiscrete)
        {
            Action0 = action0;
            Action1 = action1;
            DefendFlags = defendFlags;
            GuardMode = guardMode;
            WasDiscrete = wasDiscrete;
        }
    }

    public AgentActionHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider)
    {
        this.client = client;
        this.packetManager = packetManager;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;

        this.packetManager.RegisterPacketHandler(this);
    }

    public PacketType PacketType => PacketType.AgentAction;

    public void PollActions()
    {
        if (Mission.Current == null) return;

        List<Guid> ids = null;
        List<AgentActionData> actions = null;

        foreach (var info in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            Agent agent = info.Agent;
            if (agent == null || agent.Mission == null || !agent.IsActive() || agent.Health <= 0)
                continue;

            // Registered MOUNTS are not action-synced: a ridden horse's channel-1 action already rides in its
            // rider's MountData (a second stream would fight it), and a masterless one isn't movement-synced
            // either — its registry entry exists for damage routing and death sync only.
            if (agent.IsMount)
                continue;

            int action0 = agent.GetCurrentAction(0).Index;
            int action1 = agent.GetCurrentAction(1).Index;
            var defendFlags = AgentActionData.GetDefendMovementFlags(agent.MovementFlags);
            Agent.GuardMode guardMode = agent.CurrentGuardMode;

            bool hadState = _lastActions.TryGetValue(info.AgentId, out var last);
            bool defendChanged;
            if (hadState)
            {
                defendChanged = last.DefendFlags != defendFlags;
            }
            else
            {
                defendChanged = defendFlags != Agent.MovementControlFlag.None;
            }

            bool guardChanged;
            if (hadState)
            {
                guardChanged = last.GuardMode != guardMode;
            }
            else
            {
                guardChanged = AgentActionData.IsGuardMode(guardMode);
            }

            if (hadState && last.Action0 == action0 && last.Action1 == action1
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
            bool broadcast = defendChanged || guardChanged || nowDiscrete || (hadState && last.WasDiscrete);

            _lastActions[info.AgentId] = new ActionState(action0, action1, defendFlags, guardMode, nowDiscrete);
            if (!broadcast)
                continue;

            (ids ??= new List<Guid>()).Add(info.AgentId);
            (actions ??= new List<AgentActionData>()).Add(new AgentActionData(agent));
        }

        if (ids == null) return;

        for (int start = 0; start < ids.Count; start += MaxAgentsPerActionPacket)
        {
            int count = Math.Min(MaxAgentsPerActionPacket, ids.Count - start);
            var idChunk = new Guid[count];
            var dataChunk = new AgentActionData[count];
            ids.CopyTo(start, idChunk, 0, count);
            actions.CopyTo(start, dataChunk, 0, count);
            client.SendAll(new AgentActionPacket(idChunk, dataChunk));
        }
    }

    public void ApplyRemoteGuardStates()
    {
        if (_disposed || Mission.Current == null || _remoteGuardStates.Count == 0) return;

        List<Guid> staleIds = null;
        using (new AllowedThread())
        {
            foreach (var guardState in _remoteGuardStates)
            {
                Guid agentId = guardState.Key;
                if (agentRegistry.IsLocallyControlled(agentId)
                    || !agentRegistry.TryGetAgentInfo(agentId, out var info))
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

                // Vanilla's cautious behavior asserts SetWeaponGuard every agent tick while guarding.
                AgentActionData.ApplyGuardState(agent, guardState.Value);
            }
        }

        if (staleIds == null) return;
        foreach (Guid agentId in staleIds)
        {
            _remoteGuardStates.Remove(agentId);
        }
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var actionPacket = (AgentActionPacket)packet;
        if (actionPacket.AgentIds == null) return;

        // Resolve and apply the whole batch in ONE game-thread action, matching AgentMovementHandler.
        // Resolving here keeps this ordered behind earlier game-thread spawn/register work.
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            using (new AllowedThread())
            {
                for (int i = 0; i < actionPacket.AgentIds.Length; i++)
                {
                    var agentId = actionPacket.AgentIds[i];
                    if (agentRegistry.IsLocallyControlled(agentId)) continue;
                    if (!agentRegistry.TryGetAgentInfo(agentId, out var info)) continue;

                    Agent agent = info.Agent;
                    AgentActionData data = actionPacket.Actions[i];

                    // The agent may have become invalid between queueing and running; only apply while active.
                    if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
                        continue;

                    data.Apply(agent);

                    Agent.GuardMode guardMode = data.GuardMode;
                    if (AgentActionData.IsGuardMode(guardMode))
                    {
                        _remoteGuardStates[agentId] = guardMode;
                    }
                    else if (_remoteGuardStates.Remove(agentId))
                    {
                        AgentActionData.ApplyGuardState(agent, Agent.GuardMode.None);
                    }
                }
            }
        });
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

        packetManager.RemovePacketHandler(this);
        _lastActions.Clear();
        _remoteGuardStates.Clear();
    }
}
