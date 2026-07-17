using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using LiteNetLib;
using Missions.Agents.Packets;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
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
    private readonly IBattleHostRegistry battleHostRegistry;
    private readonly IMissionContext missionContext;

    // Last observed action indices, defend input, and guard state per owned agent, so we broadcast only on change. WasDiscrete
    // lets us also send the END of a discrete action while still skipping locomotion<->locomotion churn.
    private readonly Dictionary<Guid, ActionState> _lastActions = new Dictionary<Guid, ActionState>();
    private readonly Dictionary<Guid, long> _localActionSequences = new Dictionary<Guid, long>();
    private readonly Dictionary<Guid, RemoteGuardState> _remoteGuardStates =
        new Dictionary<Guid, RemoteGuardState>();
    private readonly Dictionary<Guid, RemoteActionSequence> _remoteActionSequences =
        new Dictionary<Guid, RemoteActionSequence>();
    private readonly Dictionary<Guid, Dictionary<string, PendingRemoteAction>> _pendingRemoteActions =
        new Dictionary<Guid, Dictionary<string, PendingRemoteAction>>();
    private readonly Dictionary<Guid, MigratedActionAuthority> _migratedActionAuthorities =
        new Dictionary<Guid, MigratedActionAuthority>();
    private readonly Dictionary<int, MigrationLineage> _migrationLineages =
        new Dictionary<int, MigrationLineage>();
    private readonly HashSet<string> _knownBattleHostControllers =
        new HashSet<string>();
    private readonly object _knownBattleHostControllersGate = new object();

    private int _appliedMigrationEpoch;
    private int _highestReceivedHostActionEpoch;
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

    private readonly struct PendingRemoteAction
    {
        public readonly string ControllerId;
        public readonly AgentActionData Data;
        public readonly long Sequence;
        public readonly int BattleHostEpoch;

        public PendingRemoteAction(
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

    public AgentActionHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IBattleHostRegistry battleHostRegistry,
        IMissionContext missionContext)
    {
        this.client = client;
        this.packetManager = packetManager;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.battleHostRegistry = battleHostRegistry;
        this.missionContext = missionContext;

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
            ClearRemoteStateForLocalAgent(info.AgentId, agent);
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
            (sequences ??= new List<long>()).Add(NextActionSequence(info.AgentId));
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

                var defendFlags = AgentActionData.GetDefendMovementFlags(agent.MovementFlags);
                if (defendFlags == Agent.MovementControlFlag.None
                    && !AgentActionData.IsGuardMode(agent.CurrentGuardMode))
                    continue;

                (ids ??= new List<Guid>()).Add(info.AgentId);
                (actions ??= new List<AgentActionData>()).Add(new AgentActionData(agent));
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
        int battleHostEpoch = GetOutgoingBattleHostEpoch();
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
        _localActionSequences.TryGetValue(agentId, out long sequence);
        sequence++;
        _localActionSequences[agentId] = sequence;
        return sequence;
    }

    private void ClearRemoteStateForLocalAgent(Guid agentId, Agent agent)
    {
        _pendingRemoteActions.Remove(agentId);
        _remoteActionSequences.Remove(agentId);
        _migratedActionAuthorities.Remove(agentId);
        if (!_remoteGuardStates.Remove(agentId)) return;
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

        if (_remoteGuardStates.Count == 0) return;

        List<Guid> staleIds = null;
        using (new AllowedThread())
        {
            foreach (var guardState in _remoteGuardStates)
            {
                Guid agentId = guardState.Key;
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
                    guardState.Value.ControllerId,
                    guardState.Value.BattleHostEpoch))
                {
                    ClearRemoteDefendState(agent);
                    (staleIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                AgentActionData.ApplyDefendMovementFlags(
                    agent,
                    guardState.Value.DefendFlags);
                if (AgentActionData.IsGuardMode(guardState.Value.GuardMode))
                {
                    AgentActionData.ApplyGuardState(
                        agent,
                        guardState.Value.GuardMode);
                }
            }
        }

        if (staleIds == null) return;
        foreach (Guid agentId in staleIds)
        {
            _remoteGuardStates.Remove(agentId);
        }
    }

    private void ApplyPendingRemoteActions()
    {
        if (_pendingRemoteActions.Count == 0) return;

        List<Guid> resolvedIds = null;
        int appliedMigrationEpoch = _appliedMigrationEpoch;
        using (new AllowedThread())
        {
            foreach (var pendingByAgent in _pendingRemoteActions)
            {
                Guid agentId = pendingByAgent.Key;
                if (agentRegistry.IsLocallyControlled(agentId))
                {
                    (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (!agentRegistry.TryGetAgentInfo(agentId, out var info))
                    continue;

                PromotePendingMigration(info, pendingByAgent.Value);
                string authority = GetCurrentActionAuthority(
                    info,
                    out int requiredHostEpoch);
                RemoveExpiredPendingActions(
                    pendingByAgent.Value,
                    authority,
                    requiredHostEpoch,
                    appliedMigrationEpoch);

                bool hasCurrentPending = pendingByAgent.Value.TryGetValue(
                    authority,
                    out PendingRemoteAction pending);
                if (!hasCurrentPending
                    || (requiredHostEpoch != 0
                        && pending.BattleHostEpoch != requiredHostEpoch)
                    || !IsCurrentActionAuthority(
                        info,
                        pending.ControllerId,
                        pending.BattleHostEpoch))
                {
                    if (hasCurrentPending
                        && pending.BattleHostEpoch <= appliedMigrationEpoch)
                    {
                        pendingByAgent.Value.Remove(pending.ControllerId);
                    }
                    if (pendingByAgent.Value.Count == 0)
                        (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                if (IsStaleRemoteAction(
                    agentId,
                    pending.ControllerId,
                    pending.Sequence,
                    pending.BattleHostEpoch))
                {
                    pendingByAgent.Value.Remove(pending.ControllerId);
                    if (pendingByAgent.Value.Count == 0)
                        (resolvedIds ??= new List<Guid>()).Add(agentId);
                    continue;
                }

                Agent agent = info.Agent;
                if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
                    continue;

                pending.Data.Apply(agent);
                RecordRemoteActionSequence(
                    agentId,
                    pending.ControllerId,
                    pending.Sequence,
                    pending.BattleHostEpoch);
                UpdateRemoteGuardState(
                    agentId,
                    pending.ControllerId,
                    pending.Data,
                    agent,
                    pending.BattleHostEpoch);
                pendingByAgent.Value.Remove(pending.ControllerId);
                if (pendingByAgent.Value.Count == 0)
                    (resolvedIds ??= new List<Guid>()).Add(agentId);
            }
        }

        if (resolvedIds == null) return;
        foreach (Guid agentId in resolvedIds)
        {
            _pendingRemoteActions.Remove(agentId);
        }
    }

    private void PromotePendingMigration(
        CoopAgentInfo info,
        Dictionary<string, PendingRemoteAction> actionsByController)
    {
        int highestReceivedEpoch = Volatile.Read(
            ref _highestReceivedHostActionEpoch);
        foreach (PendingRemoteAction pending in actionsByController.Values)
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

            if (_migratedActionAuthorities.TryGetValue(
                info.AgentId,
                out MigratedActionAuthority existing)
                && existing.BattleHostEpoch > pending.BattleHostEpoch)
            {
                continue;
            }

            _migratedActionAuthorities[info.AgentId] =
                new MigratedActionAuthority(
                    info.CurrentAuthority,
                    pending.ControllerId,
                    pending.BattleHostEpoch);
        }
    }

    private void UpdateRemoteGuardState(
        Guid agentId,
        string controllerId,
        AgentActionData data,
        Agent agent,
        int battleHostEpoch)
    {
        Agent.MovementControlFlag defendFlags = data.DefendFlags;
        Agent.GuardMode guardMode = data.GuardMode;
        if (defendFlags != Agent.MovementControlFlag.None
            || AgentActionData.IsGuardMode(guardMode))
        {
            _remoteGuardStates[agentId] = new RemoteGuardState(
                controllerId,
                defendFlags,
                guardMode,
                battleHostEpoch);
        }
        else
        {
            _remoteGuardStates.Remove(agentId);
            ClearRemoteDefendState(agent);
        }
    }

    private static void ClearRemoteDefendState(Agent agent)
    {
        AgentActionData.ApplyDefendMovementFlags(
            agent,
            Agent.MovementControlFlag.None);
        AgentActionData.ApplyGuardState(agent, Agent.GuardMode.None);
    }

    private void BufferPendingRemoteAction(
        Guid agentId,
        string controllerId,
        long sequence,
        AgentActionData data,
        int battleHostEpoch)
    {
        if (battleHostEpoch > 0
            && battleHostEpoch < Volatile.Read(
                ref _highestReceivedHostActionEpoch))
        {
            return;
        }

        if (!_pendingRemoteActions.TryGetValue(agentId, out var actionsByController))
        {
            actionsByController = new Dictionary<string, PendingRemoteAction>();
            _pendingRemoteActions[agentId] = actionsByController;
        }

        if (actionsByController.TryGetValue(controllerId, out var existing))
        {
            if (existing.BattleHostEpoch > battleHostEpoch)
                return;
            if (existing.BattleHostEpoch == battleHostEpoch
                && existing.Sequence >= sequence)
                return;
        }

        actionsByController[controllerId] =
            new PendingRemoteAction(
                controllerId,
                data,
                sequence,
                battleHostEpoch);
    }

    private bool IsStaleRemoteAction(
        Guid agentId,
        string controllerId,
        long sequence,
        int battleHostEpoch)
    {
        return _remoteActionSequences.TryGetValue(agentId, out var last)
            && last.ControllerId == controllerId
            && last.BattleHostEpoch == battleHostEpoch
            && last.Sequence >= sequence;
    }

    private bool HasPendingRemoteActionAtOrAfter(
        Guid agentId,
        string controllerId,
        long sequence,
        int battleHostEpoch)
    {
        return _pendingRemoteActions.TryGetValue(agentId, out var actionsByController)
            && actionsByController.TryGetValue(controllerId, out var pending)
            && pending.BattleHostEpoch == battleHostEpoch
            && pending.Sequence >= sequence;
    }

    private void RecordRemoteActionSequence(
        Guid agentId,
        string controllerId,
        long sequence,
        int battleHostEpoch)
    {
        _remoteActionSequences[agentId] = new RemoteActionSequence(
            controllerId,
            sequence,
            battleHostEpoch);
    }

    private void RemovePendingRemoteAction(
        Guid agentId,
        string controllerId,
        int battleHostEpoch)
    {
        if (!_pendingRemoteActions.TryGetValue(agentId, out var actionsByController))
            return;

        if (actionsByController.TryGetValue(controllerId, out var pending)
            && pending.BattleHostEpoch == battleHostEpoch)
        {
            actionsByController.Remove(controllerId);
        }
        if (actionsByController.Count == 0)
            _pendingRemoteActions.Remove(agentId);
    }

    private void RemoveExpiredPendingActions(
        Dictionary<string, PendingRemoteAction> actionsByController,
        string currentAuthority,
        int requiredHostEpoch,
        int appliedMigrationEpoch)
    {
        List<string> expiredControllers = null;
        foreach (var pendingByController in actionsByController)
        {
            PendingRemoteAction pending = pendingByController.Value;
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
        if (_migratedActionAuthorities.TryGetValue(
            info.AgentId,
            out MigratedActionAuthority migrated))
        {
            if (migrated.ObservedAuthority == info.CurrentAuthority)
            {
                requiredHostEpoch = migrated.BattleHostEpoch;
                return migrated.ControllerId;
            }

            _migratedActionAuthorities.Remove(info.AgentId);
        }

        return info.CurrentAuthority;
    }

    private int GetOutgoingBattleHostEpoch()
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

    private void Handle_BattleHostAssigned(
        MessagePayload<NetworkBattleHostAssigned> payload)
    {
        if (_disposed) return;

        NetworkBattleHostAssigned message = payload.What;
        if (message.MapEventId != BattleSpawnGate.ActiveMapEventId)
            return;
        if (!battleHostRegistry.TryGet(message.MapEventId, out var assignment)
            || assignment.Epoch != message.Epoch
            || assignment.HostControllerId != message.HostControllerId)
            return;

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
            foreach (var migratedByAgent in _migratedActionAuthorities)
            {
                if (migratedByAgent.Value.BattleHostEpoch <= hostEpoch
                    && directSourceAuthorities.Contains(
                        migratedByAgent.Value.ControllerId))
                {
                    (inheritedAgentIds ??= new List<Guid>())
                        .Add(migratedByAgent.Key);
                }
            }

            foreach (string observedAuthority in directSourceAuthorities)
            {
                foreach (CoopAgentInfo info in agentRegistry.GetAgents(
                    observedAuthority))
                {
                    if (_migratedActionAuthorities.TryGetValue(
                        info.AgentId,
                        out MigratedActionAuthority existing)
                        && existing.BattleHostEpoch > hostEpoch)
                    {
                        continue;
                    }

                    _migratedActionAuthorities[info.AgentId] =
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
                    MigratedActionAuthority inherited =
                        _migratedActionAuthorities[agentId];
                    _migratedActionAuthorities[agentId] =
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

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var actionPacket = (AgentActionPacket)packet;
        if (actionPacket.AgentIds == null
            || actionPacket.Actions == null
            || actionPacket.Sequences == null
            || actionPacket.AgentIds.Length != actionPacket.Actions.Length
            || actionPacket.AgentIds.Length != actionPacket.Sequences.Length
            || string.IsNullOrEmpty(actionPacket.ControllerId))
            return;

        ObserveHostActionEpoch(actionPacket.BattleHostEpoch);

        // Resolve and apply the whole batch in ONE game-thread action, matching AgentMovementHandler.
        // Resolving here keeps this ordered behind earlier game-thread spawn/register work.
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            bool shouldBufferForHostAssignment =
                ShouldBufferForHostAssignment(actionPacket);
            using (new AllowedThread())
            {
                for (int i = 0; i < actionPacket.AgentIds.Length; i++)
                {
                    var agentId = actionPacket.AgentIds[i];
                    AgentActionData data = actionPacket.Actions[i];
                    long sequence = actionPacket.Sequences[i];
                    if (sequence <= 0)
                        continue;

                    if (agentRegistry.IsLocallyControlled(agentId))
                    {
                        _pendingRemoteActions.Remove(agentId);
                        continue;
                    }

                    if (!agentRegistry.TryGetAgentInfo(agentId, out var info))
                    {
                        BufferPendingRemoteAction(
                            agentId,
                            actionPacket.ControllerId,
                            sequence,
                            data,
                            actionPacket.BattleHostEpoch);
                        continue;
                    }

                    if (!IsCurrentActionAuthority(
                        info,
                        actionPacket.ControllerId,
                        actionPacket.BattleHostEpoch))
                    {
                        if (shouldBufferForHostAssignment)
                        {
                            BufferPendingRemoteAction(
                                agentId,
                                actionPacket.ControllerId,
                                sequence,
                                data,
                                actionPacket.BattleHostEpoch);
                        }

                        continue;
                    }

                    if (HasPendingRemoteActionAtOrAfter(
                        agentId,
                        actionPacket.ControllerId,
                        sequence,
                        actionPacket.BattleHostEpoch))
                    {
                        continue;
                    }

                    if (IsStaleRemoteAction(
                        agentId,
                        actionPacket.ControllerId,
                        sequence,
                        actionPacket.BattleHostEpoch))
                    {
                        continue;
                    }

                    Agent agent = info.Agent;

                    // The agent may have become invalid between queueing and running; only apply while active.
                    if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
                    {
                        BufferPendingRemoteAction(
                            agentId,
                            actionPacket.ControllerId,
                            sequence,
                            data,
                            actionPacket.BattleHostEpoch);
                        continue;
                    }

                    RemovePendingRemoteAction(
                        agentId,
                        actionPacket.ControllerId,
                        actionPacket.BattleHostEpoch);
                    data.Apply(agent);
                    RecordRemoteActionSequence(
                        agentId,
                        actionPacket.ControllerId,
                        sequence,
                        actionPacket.BattleHostEpoch);
                    UpdateRemoteGuardState(
                        agentId,
                        actionPacket.ControllerId,
                        data,
                        agent,
                        actionPacket.BattleHostEpoch);
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

        messageBroker.Unsubscribe<NetworkBattleHostAssigned>(Handle_BattleHostAssigned);
        packetManager.RemovePacketHandler(this);
        _lastActions.Clear();
        _localActionSequences.Clear();
        _remoteGuardStates.Clear();
        _remoteActionSequences.Clear();
        _pendingRemoteActions.Clear();
        _migratedActionAuthorities.Clear();
        _migrationLineages.Clear();
        lock (_knownBattleHostControllersGate)
        {
            _knownBattleHostControllers.Clear();
        }
    }
}
