using Common;
using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using LiteNetLib;
using Missions.Agents;
using Missions.Agents.Packets;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using AgentControllerType = TaleWorlds.Core.AgentControllerType;

namespace Missions.Agents.Handlers;

public interface IAgentMovementHandler : IPacketHandler, IDisposable
{
    /// <summary>
    /// [Game thread] Capture owned agents' continuous movement state and broadcast it to peers.
    /// </summary>
    void PollMovement(float dt);

    /// <summary>Per-frame position smoother for received puppets; ticked by CoopMissionController.OnMissionTick.</summary>
    IAgentPositionInterpolator Interpolator { get; }

    /// <summary>Replays authoritative synthetic mount turns after native agent processing.</summary>
    void ReplaySyntheticMountTurnAnimationsAfterNativeTick();

    /// <summary>Receive side for masterless-horse movement (<see cref="MountMovementPacket"/>); the send side
    /// is this handler's movement tick. Exposed so the packet flow is reachable in tests.</summary>
    IPacketHandler MountMovementApplier { get; }
}

public class AgentMovementHandler : IAgentMovementHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentMovementHandler>();

    // Movement is strictly droppable, so keep even mounted snapshots below the 1 KB unreliable ceiling.
    private const int MaxAgentsPerMovementPacket = 3;

    // Forty updates per second keeps locally authoritative agents responsive.
    private const float MovementPollingIntervalSeconds = 0.025f;
    private const int SyntheticMountTurnPollLimit = 80;
    private const int RemoteSyntheticMountTurnClearGraceFrames = 3;
    private const float UseActionBlendPeriod = -0.2f;

    private readonly IPacketManager packetManager;
    private readonly IBattleNetwork client;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IAgentEquipmentApplier equipmentApplier;
    private readonly IPuppetMountStateRepairer puppetMountStateRepairer;
    private readonly IAgentVisualActionAccessor visualActionAccessor;
    private readonly Dictionary<Guid, AgentEquipmentData> lastEquipment = new Dictionary<Guid, AgentEquipmentData>();

    // A puppet's horse, remembered when its owner dismounts, so a later re-mount can put it back on the
    // same one. Touched only on the game thread (inside HandlePacket's apply), so no lock; per-mission
    // (this handler is transient), so it can't leak across missions.
    private readonly Dictionary<Agent, Agent> _dismountedHorses = new Dictionary<Agent, Agent>();

    // Vanilla can rotate a stopped AI mount's facing without selecting a turn action.
    private readonly Dictionary<Agent, Vec2> _lastMountDirections = new Dictionary<Agent, Vec2>();
    private readonly Dictionary<Agent, SyntheticMountTurnState> _syntheticMountTurns =
        new Dictionary<Agent, SyntheticMountTurnState>();
    private readonly Dictionary<Agent, RemoteSyntheticMountTurnState> _remoteSyntheticMountTurns =
        new Dictionary<Agent, RemoteSyntheticMountTurnState>();
    private readonly List<Agent> _completedRemoteSyntheticMountTurns =
        new List<Agent>();

    private sealed class SyntheticMountTurnState
    {
        public readonly int Direction;
        public readonly Agent Rider;
        public readonly int ActionIndex;
        public int ElapsedPolls;

        public SyntheticMountTurnState(
            int direction,
            Agent rider,
            int actionIndex)
        {
            Direction = direction;
            Rider = rider;
            ActionIndex = actionIndex;
        }

        public float Progress =>
            Math.Min(0.99f, (float)ElapsedPolls / SyntheticMountTurnPollLimit);
    }

    private sealed class RemoteSyntheticMountTurnState
    {
        public readonly int Direction;
        public readonly int ActionIndex;
        public float Progress;
        public int FramesSinceUpdate;
        public bool ClearRequested;

        public RemoteSyntheticMountTurnState(
            int direction,
            int actionIndex,
            float progress)
        {
            Direction = direction;
            ActionIndex = actionIndex;
            Progress = progress;
        }
    }

    // Per-frame position smoothing for received puppets. Fed the latest target on each packet apply (below) and
    // ticked from CoopMissionController.OnMissionTick, so the ease is decoupled from the bursty poll cadence.
    private readonly AgentPositionInterpolator _interpolator = new AgentPositionInterpolator();
    public IAgentPositionInterpolator Interpolator => _interpolator;

    // Masterless-horse movement receive side. Owned here (registered/removed with this handler) so both
    // movement streams share one deterministic lifecycle; PollMovement is its send side.
    private readonly MountMovementApplier _mountMovementApplier;
    public IPacketHandler MountMovementApplier => _mountMovementApplier;

    // Dispose is called deterministically on mission teardown (CoopMissionController.OnEndMissionInternal); this
    // guards against a second call (the GC finalizer, or the DI scope also disposing this transient handler).
    private bool _disposed;
    private float movementPollElapsed = MovementPollingIntervalSeconds;

    public AgentMovementHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IAgentEquipmentApplier equipmentApplier,
        IPuppetMountStateRepairer puppetMountStateRepairer,
        IAgentVisualActionAccessor visualActionAccessor)
    {
        Logger.Verbose("Creating {handlerType}", typeof(AgentMovementHandler));

        this.packetManager = packetManager;
        this.client = client;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.equipmentApplier = equipmentApplier;
        this.puppetMountStateRepairer = puppetMountStateRepairer;
        this.visualActionAccessor = visualActionAccessor;

        // Server-mediated membership. A peer entering is the cue to clear any STALE party it left behind
        // on a missed disconnect (so its rejoin re-spawns clean); a leave/disconnect releases its party.
        this.messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        this.messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        this.messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        this.packetManager.RegisterPacketHandler(this);
        this.packetManager.RegisterPacketHandler(equipmentApplier);

        _mountMovementApplier = new MountMovementApplier(agentRegistry, _interpolator, puppetMountStateRepairer);
        this.packetManager.RegisterPacketHandler(_mountMovementApplier);
    }

    // Safety net only: with deterministic disposal on mission end the finalizer is suppressed and never runs.
    ~AgentMovementHandler()
    {
        Dispose();
    }

    /// <summary>
    /// Deterministic teardown, called from <c>CoopMissionController.OnEndMissionInternal</c> at the start of the
    /// leave path. Detaches from the packet manager and message broker. Idempotent — safe if the GC finalizer or
    /// the DI scope disposes this handler again.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Logger.Verbose("Disposing {handlerType}", typeof(AgentMovementHandler));

        _interpolator.Clear();
        _lastMountDirections.Clear();
        _syntheticMountTurns.Clear();
        _remoteSyntheticMountTurns.Clear();
        _completedRemoteSyntheticMountTurns.Clear();

        packetManager.RemovePacketHandler(this);
        packetManager.RemovePacketHandler(_mountMovementApplier);
        packetManager.RemovePacketHandler(equipmentApplier);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        // Disposed explicitly, so the finalizer no longer needs to run.
        GC.SuppressFinalize(this);
    }

    public PacketType PacketType => PacketType.Movement;

    private sealed class MovementBatch<T>
    {
        public readonly string IdentityScopeId;
        public readonly List<ushort> CompactIds = new List<ushort>();
        public readonly List<Guid> CanonicalIds = new List<Guid>();
        public readonly List<T> Data = new List<T>();

        public MovementBatch(string identityScopeId)
        {
            IdentityScopeId = identityScopeId;
        }

        public void Add(CoopAgentInfo info, T data)
        {
            if (IdentityScopeId == null)
                CanonicalIds.Add(info.AgentId);
            else
                CompactIds.Add(info.MovementId);
            Data.Add(data);
        }
    }

    // Broadcast every locally authoritative agent.
    public void PollMovement(float dt)
    {
        if (_disposed || Mission.Current == null) return;

        movementPollElapsed += dt;
        if (movementPollElapsed < MovementPollingIntervalSeconds) return;
        movementPollElapsed %= MovementPollingIntervalSeconds;

        var movementGroups = new Dictionary<string, MovementBatch<AgentData>>();
        var mountGroups = new Dictionary<string, MovementBatch<AgentMountData>>();
        var equipmentGroups = new Dictionary<string, MovementBatch<AgentEquipmentData>>();
        MovementBatch<AgentData> legacyMovement = null;
        MovementBatch<AgentMountData> legacyMountMovement = null;
        MovementBatch<AgentEquipmentData> legacyEquipment = null;

        foreach (var agentInfo in agentRegistry.GetAgents(controllerIdProvider.ControllerId))
        {
            Agent agent = agentInfo.Agent;
            // Skip agents whose native object is already gone (dead/removed but not yet deregistered):
            // building the snapshot calls into the agent, which can access-violate after native teardown.
            if (agent == null || agent.Mission == null || !agent.IsActive()) continue;

            EnsureLocallyDrivenMountController(agent);
            if (!ShouldBroadcastMovement(agent)) continue;

            if (agent.IsMount)
            {
                int turnDirection = EnsureStationaryMountTurnAnimation(
                    agent,
                    out int turnActionIndex,
                    out float turnProgress,
                    out bool syntheticTurn);
                AddToBatch(
                    mountGroups,
                    ref legacyMountMovement,
                    agentInfo,
                    new AgentMountData(
                        agent,
                        mountAction0TurnDirection: turnDirection == AgentMountData.NoTurn
                            ? (int?)null
                            : turnDirection,
                        mountAction0TurnActionIndex: turnDirection == AgentMountData.NoTurn
                            ? (int?)null
                            : turnActionIndex,
                        mountAction0TurnProgress: turnDirection == AgentMountData.NoTurn
                            ? (float?)null
                            : turnProgress,
                        mountAction0IsSyntheticTurn: syntheticTurn));
            }
            else
            {
                GetRegisteredMountIdentity(
                    agent,
                    agentInfo.MovementScopeId,
                    out ushort mountMovementId,
                    out string mountIdentityScopeId,
                    out Guid mountAgentId);
                Agent mount = agent.MountAgent;
                int turnDirection = EnsureStationaryMountTurnAnimation(
                    mount,
                    out int turnActionIndex,
                    out float turnProgress,
                    out bool syntheticTurn);
                AddToBatch(
                    movementGroups,
                    ref legacyMovement,
                    agentInfo,
                    new AgentData(
                        agent,
                        mountMovementId,
                        mountIdentityScopeId,
                        mountAgentId,
                        turnDirection == AgentMountData.NoTurn ? (int?)null : turnDirection,
                        turnDirection == AgentMountData.NoTurn ? (int?)null : turnActionIndex,
                        turnDirection == AgentMountData.NoTurn ? (float?)null : turnProgress,
                        mountAction0IsSyntheticTurn: syntheticTurn));

                var equipment = new AgentEquipmentData(agent);
                if (!lastEquipment.TryGetValue(agentInfo.AgentId, out var previousEquipment))
                {
                    lastEquipment[agentInfo.AgentId] = equipment;

                    // Battle spawn/catch-up records already carry the current wield state. Compact ids are
                    // battle-only, so seeding their cache here avoids immediately resending every agent's
                    // equipment on the first 40 Hz poll. Legacy Guid registrations still need an initial update.
                    if (agentInfo.MovementId != 0)
                        continue;
                }
                else if (previousEquipment.Equals(equipment))
                {
                    continue;
                }

                lastEquipment[agentInfo.AgentId] = equipment;
                AddToBatch(
                    equipmentGroups,
                    ref legacyEquipment,
                    agentInfo,
                    equipment);
            }
        }

        SendEquipment(equipmentGroups.Values);
        SendEquipment(legacyEquipment);
        SendMovement(movementGroups.Values);
        SendMovement(legacyMovement);
        SendMountMovement(mountGroups.Values);
        SendMountMovement(legacyMountMovement);
    }

    public void ReplaySyntheticMountTurnAnimationsAfterNativeTick()
    {
        foreach (var pair in _syntheticMountTurns)
        {
            Agent mount = pair.Key;
            SyntheticMountTurnState syntheticTurn = pair.Value;
            if (mount == null
                || mount.Mission == null
                || !mount.IsActive())
            {
                continue;
            }

            visualActionAccessor.TrySetAction(
                mount,
                0,
                new ActionIndexCache(syntheticTurn.ActionIndex),
                syntheticTurn.Progress,
                UseActionBlendPeriod);
        }

        _completedRemoteSyntheticMountTurns.Clear();
        foreach (var pair in _remoteSyntheticMountTurns)
        {
            Agent mount = pair.Key;
            RemoteSyntheticMountTurnState syntheticTurn = pair.Value;
            if (mount == null
                || mount.Mission == null
                || !mount.IsActive())
            {
                _completedRemoteSyntheticMountTurns.Add(mount);
                continue;
            }

            visualActionAccessor.TrySetAction(
                mount,
                0,
                new ActionIndexCache(syntheticTurn.ActionIndex),
                syntheticTurn.Progress,
                UseActionBlendPeriod);
            syntheticTurn.FramesSinceUpdate++;
            if (syntheticTurn.ClearRequested
                && syntheticTurn.FramesSinceUpdate
                    >= RemoteSyntheticMountTurnClearGraceFrames)
            {
                _completedRemoteSyntheticMountTurns.Add(mount);
            }
        }

        foreach (Agent mount in _completedRemoteSyntheticMountTurns)
            _remoteSyntheticMountTurns.Remove(mount);
    }

    private void UpdateRemoteSyntheticMountTurn(
        Agent mount,
        AgentMountData mountData)
    {
        if (mount == null) return;

        bool syntheticTurn =
            mountData != null
            && mountData.MountSpeed <= AgentMountData.StationarySpeedThreshold
            && mountData.MountAction0TurnDirection != AgentMountData.NoTurn
            && mountData.MountAction0TurnActionIndex
                != AgentMountData.NoActionIndex;
        if (!syntheticTurn)
        {
            if (_remoteSyntheticMountTurns.TryGetValue(
                    mount,
                    out RemoteSyntheticMountTurnState completedTurn))
            {
                completedTurn.ClearRequested = true;
            }
            return;
        }

        if (!_remoteSyntheticMountTurns.TryGetValue(
                mount,
                out RemoteSyntheticMountTurnState activeTurn)
            || activeTurn.Direction
                != mountData.MountAction0TurnDirection
            || activeTurn.ActionIndex
                != mountData.MountAction0TurnActionIndex)
        {
            _remoteSyntheticMountTurns[mount] =
                new RemoteSyntheticMountTurnState(
                    mountData.MountAction0TurnDirection,
                    mountData.MountAction0TurnActionIndex,
                    mountData.MountAction0Progress);
            return;
        }

        if (mountData.MountAction0Progress > activeTurn.Progress)
            activeTurn.Progress = mountData.MountAction0Progress;
        activeTurn.FramesSinceUpdate = 0;
        activeTurn.ClearRequested = false;
    }

    private int EnsureStationaryMountTurnAnimation(
        Agent mount,
        out int turnActionIndex,
        out float turnProgress,
        out bool syntheticTurn)
    {
        turnActionIndex = AgentMountData.NoActionIndex;
        turnProgress = 0f;
        syntheticTurn = false;
        if (mount == null || !mount.IsActive()) return AgentMountData.NoTurn;

        Vec2 currentDirection = mount.GetMovementDirection();
        bool hasPreviousDirection = _lastMountDirections.TryGetValue(
            mount,
            out Vec2 previousDirection);
        ActionIndexCache currentAction = mount.GetCurrentAction(0);
        if (mount.GetRealGlobalVelocity().AsVec2.Length > AgentMountData.StationarySpeedThreshold)
        {
            ClearSyntheticMountTurn(mount);
            bool drivenLocomotion =
                mount.MovementInputVector.LengthSquared > 0.0001f
                || AgentMountData.IsLocomotionAction(
                    currentAction.Index,
                    null);
            if (!hasPreviousDirection || drivenLocomotion)
            {
                // Retain the final moving facing for the first stationary snapshot.
                _lastMountDirections[mount] = currentDirection;
            }
            return AgentMountData.NoTurn;
        }

        int activeTurnDirection = AgentMountData.GetStationaryTurnDirection(
            currentAction.Index);
        if (_syntheticMountTurns.TryGetValue(
                mount,
                out SyntheticMountTurnState activeSyntheticTurn))
        {
            if (activeTurnDirection != AgentMountData.NoTurn
                && currentAction.Index != activeSyntheticTurn.ActionIndex)
            {
                ClearSyntheticMountTurn(mount);
                _lastMountDirections[mount] = currentDirection;
                turnActionIndex = currentAction.Index;
                turnProgress = mount.GetCurrentActionProgress(0);
                return activeTurnDirection;
            }

            if (!ReferenceEquals(
                    activeSyntheticTurn.Rider,
                    mount.RiderAgent))
            {
                int direction = activeSyntheticTurn.Direction;
                int actionIndex = activeSyntheticTurn.ActionIndex;
                ClearSyntheticMountTurn(mount);
                StartSyntheticMountTurn(
                    mount,
                    direction,
                    actionIndex);
                activeSyntheticTurn = _syntheticMountTurns[mount];
            }

            activeSyntheticTurn.ElapsedPolls++;
            if (activeSyntheticTurn.ElapsedPolls
                >= SyntheticMountTurnPollLimit)
            {
                ClearSyntheticMountTurn(mount);
                _lastMountDirections[mount] = currentDirection;
                return AgentMountData.NoTurn;
            }

            ApplySyntheticTurnFlag(
                mount,
                activeSyntheticTurn.Direction);
            ApplySyntheticTurnFlag(
                activeSyntheticTurn.Rider,
                activeSyntheticTurn.Direction);
            turnActionIndex = activeSyntheticTurn.ActionIndex;
            turnProgress = activeSyntheticTurn.Progress;
            syntheticTurn = true;
            return activeSyntheticTurn.Direction;
        }

        if (activeTurnDirection != AgentMountData.NoTurn)
        {
            _lastMountDirections[mount] = currentDirection;
            turnActionIndex = currentAction.Index;
            turnProgress = mount.GetCurrentActionProgress(0);
            return activeTurnDirection;
        }

        if (!hasPreviousDirection)
        {
            _lastMountDirections[mount] = currentDirection;
            return AgentMountData.NoTurn;
        }

        int turnDirection = AgentMountData.GetTurnDirection(previousDirection, currentDirection);
        if (turnDirection == AgentMountData.NoTurn)
            return AgentMountData.NoTurn;

        string desiredActionName = AgentMountData.GetStationaryTurnActionName(
            null,
            mount.Monster?.MonsterUsage,
            turnDirection);
        ActionIndexCache desiredAction = ActionIndexCache.Create(desiredActionName);
        turnActionIndex = desiredAction.Index;
        StartSyntheticMountTurn(
            mount,
            turnDirection,
            desiredAction.Index);
        _lastMountDirections[mount] = currentDirection;
        syntheticTurn = true;
        return turnDirection;
    }

    private void StartSyntheticMountTurn(
        Agent mount,
        int turnDirection,
        int actionIndex)
    {
        Agent rider = mount.RiderAgent;
        ApplySyntheticTurnFlag(mount, turnDirection);
        ApplySyntheticTurnFlag(rider, turnDirection);
        _syntheticMountTurns[mount] =
            new SyntheticMountTurnState(
                turnDirection,
                rider,
                actionIndex);
    }

    private static void ApplySyntheticTurnFlag(Agent agent, int turnDirection)
    {
        if (agent == null || !agent.IsActive()) return;

        Agent.MovementControlFlag movementFlags =
            AgentData.GetLocomotionMovementFlags(agent.MovementFlags);
        movementFlags = AgentMountData.WithStationaryTurnMovementFlag(
            movementFlags,
            turnDirection);
        AgentData.ApplyLocomotionMovementFlags(agent, movementFlags);
    }

    private void ClearSyntheticMountTurn(Agent mount)
    {
        if (!_syntheticMountTurns.TryGetValue(
                mount,
                out SyntheticMountTurnState syntheticTurn))
            return;

        ClearSyntheticTurnFlag(mount, syntheticTurn.Direction);
        ClearSyntheticTurnFlag(syntheticTurn.Rider, syntheticTurn.Direction);
        _syntheticMountTurns.Remove(mount);
    }

    private static void ClearSyntheticTurnFlag(
        Agent agent,
        int turnDirection)
    {
        if (agent == null || !agent.IsActive()) return;

        Agent.MovementControlFlag movementFlags =
            AgentData.GetLocomotionMovementFlags(agent.MovementFlags);
        movementFlags &= ~AgentMountData.GetStationaryTurnMovementFlag(
            turnDirection);
        AgentData.ApplyLocomotionMovementFlags(agent, movementFlags);
    }

    private static void AddToBatch<T>(
        Dictionary<string, MovementBatch<T>> compactBatches,
        ref MovementBatch<T> legacyBatch,
        CoopAgentInfo agentInfo,
        T data)
    {
        MovementBatch<T> batch;
        if (agentInfo.MovementId == 0)
        {
            batch = legacyBatch ??= new MovementBatch<T>(null);
        }
        else if (!compactBatches.TryGetValue(agentInfo.MovementScopeId, out batch))
        {
            batch = new MovementBatch<T>(agentInfo.MovementScopeId);
            compactBatches[agentInfo.MovementScopeId] = batch;
        }

        batch.Add(agentInfo, data);
    }

    private void SendEquipment(IEnumerable<MovementBatch<AgentEquipmentData>> batches)
    {
        foreach (var batch in batches)
            SendEquipment(batch);
    }

    private void SendEquipment(MovementBatch<AgentEquipmentData> batch)
    {
        if (batch == null) return;

        const int maxEquipmentPerPacket = 64;
        for (int start = 0; start < batch.Data.Count; start += maxEquipmentPerPacket)
        {
            int count = Math.Min(maxEquipmentPerPacket, batch.Data.Count - start);
            var equipment = new AgentEquipmentData[count];
            batch.Data.CopyTo(start, equipment, 0, count);

            if (batch.IdentityScopeId == null)
            {
                var ids = new Guid[count];
                batch.CanonicalIds.CopyTo(start, ids, 0, count);
                client.SendAll(new AgentEquipmentPacket(ids, equipment));
            }
            else
            {
                var ids = new ushort[count];
                batch.CompactIds.CopyTo(start, ids, 0, count);
                client.SendAll(new AgentEquipmentPacket(
                    batch.IdentityScopeId, ids, equipment));
            }
        }
    }

    private void SendMovement(IEnumerable<MovementBatch<AgentData>> batches)
    {
        foreach (var batch in batches)
            SendMovement(batch);
    }

    private void SendMovement(MovementBatch<AgentData> batch)
    {
        if (batch == null) return;

        for (int start = 0; start < batch.Data.Count; start += MaxAgentsPerMovementPacket)
        {
            int count = Math.Min(MaxAgentsPerMovementPacket, batch.Data.Count - start);
            var data = new AgentData[count];
            batch.Data.CopyTo(start, data, 0, count);

            if (batch.IdentityScopeId == null)
            {
                var ids = new Guid[count];
                batch.CanonicalIds.CopyTo(start, ids, 0, count);
                client.SendAll(new MovementPacket(ids, data));
            }
            else
            {
                var ids = new ushort[count];
                batch.CompactIds.CopyTo(start, ids, 0, count);
                client.SendAll(new MovementPacket(batch.IdentityScopeId, ids, data));
            }
        }
    }

    private void SendMountMovement(IEnumerable<MovementBatch<AgentMountData>> batches)
    {
        foreach (var batch in batches)
            SendMountMovement(batch);
    }

    private void SendMountMovement(MovementBatch<AgentMountData> batch)
    {
        if (batch == null) return;

        for (int start = 0; start < batch.Data.Count; start += MaxAgentsPerMovementPacket)
        {
            int count = Math.Min(MaxAgentsPerMovementPacket, batch.Data.Count - start);
            var data = new AgentMountData[count];
            batch.Data.CopyTo(start, data, 0, count);

            if (batch.IdentityScopeId == null)
            {
                var ids = new Guid[count];
                batch.CanonicalIds.CopyTo(start, ids, 0, count);
                client.SendAll(new MountMovementPacket(ids, data));
            }
            else
            {
                var ids = new ushort[count];
                batch.CompactIds.CopyTo(start, ids, 0, count);
                client.SendAll(new MountMovementPacket(
                    batch.IdentityScopeId, ids, data));
            }
        }
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var movement = (MovementPacket)packet;
        int idCount = movement.AgentIds?.Length ?? movement.AgentGuids?.Length ?? 0;
        if (idCount == 0 || movement.Agents == null ||
            movement.Agents.Length != idCount)
        {
            return;
        }

        // Resolve and apply the whole batch in ONE game-thread action. Resolving here keeps this ordered behind
        // earlier game-thread spawn/register work that may have been queued by reliable messages.
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            using (new AllowedThread())
            {
                for (int i = 0; i < idCount; i++)
                {
                    CoopAgentInfo agentInfo;
                    bool found = movement.AgentIds != null
                        ? agentRegistry.TryGetAgentInfo(
                            movement.IdentityScopeId, movement.AgentIds[i], out agentInfo)
                        : agentRegistry.TryGetAgentInfo(
                            movement.AgentGuids[i], out agentInfo);
                    if (!found) continue;

                    Agent agent = agentInfo.Agent;
                    AgentData data = movement.Agents[i];

                    // The agent may have become invalid (player left, mission torn down) between queueing
                    // and running; only apply while it is still active in the current mission.
                    if (agent == null || agent.Mission != Mission.Current || agent.IsActive() == false)
                        continue;

                    // Re-check authority ON the game thread: a packet from the previous owner can be queued
                    // behind a host-migration adoption (both are game-thread actions queued from the network
                    // thread), and applying it after the transfer would re-pin the freshly adopted agent to a
                    // stale position/input snapshot the AI then fights.
                    if (agentRegistry.IsLocallyControlled(agent))
                        continue;

                    Agent previousMount = agent.MountAgent;
                    SyncMountState(agent, movement.IdentityScopeId, data);
                    if (previousMount != null
                        && !ReferenceEquals(previousMount, agent.MountAgent))
                    {
                        UpdateRemoteSyntheticMountTurn(
                            previousMount,
                            null);
                    }

                    // A puppet horse must not run local AI between owner snapshots and fight their heading/input.
                    if (agent.MountAgent is Agent puppetMount && puppetMount.Controller != AgentControllerType.None)
                    {
                        puppetMount.Controller = AgentControllerType.None;
                        puppetMountStateRepairer.PreserveRiderlessPuppet(puppetMount);
                    }

                    data.Apply(agent);
                    if (data.MountData != null
                        && agent.MountAgent is Agent remoteMount)
                    {
                        UpdateRemoteSyntheticMountTurn(
                            remoteMount,
                            data.MountData);
                    }

                    // Position is reconciled per-frame by the interpolator (smoother than a per-packet
                    // correction bound to the ~10ms poll cadence); push the latest targets it eases toward.
                    if (agent.HasMount && data.MountData != null)
                    {
                        // Mounted: feed target frames to the rider, since the mount is driven through its rider.
                        // Keep the mount position only for rare large-gap snaps and drop any stale direct-horse
                        // target from before the puppet mounted.
                        _interpolator.Forget(agent.MountAgent);
                        _interpolator.SetMountedRiderTarget(agent, data);
                    }
                    else
                    {
                        _interpolator.SetRiderTarget(agent, data);
                    }
                }
            }
        });
    }

    // [Game thread] Replicate the owner's mount/dismount onto its puppet. The per-tick AgentData reports
    // whether the owner is mounted (MountData != null) and WHICH horse (MountId, when it's registered);
    // without acting on the transition a puppet stays stuck on its horse after the owner dismounts (and
    // never re-mounts). MountAgent is set directly (controller-independent — puppets have no controller to
    // process a mount/dismount input flag); the movement sync then keeps the rider/horse positioned.
    // AgentData.Apply still syncs the mount's pose while both are mounted.
    private void SyncMountState(
        Agent agent,
        string riderIdentityScopeId,
        AgentData data)
    {
        bool ownerMounted = data.MountData != null;

        if (!ownerMounted && agent.HasMount)
        {
            // Owner dismounted: get the puppet off the horse. Remember the horse for a possible re-mount, and
            // stop interpolating it (its target is no longer being reported).
            Agent horse = agent.MountAgent;
            _dismountedHorses[agent] = horse;
            _interpolator.Forget(horse);
            agent.MountAgent = null;
            RestoreLocallyControlledMount(horse);
        }
        else if (ownerMounted && !agent.HasMount)
        {
            // Owner (re)mounted: prefer the exact horse it reports (registered mounts carry their id); fall
            // back to the one the puppet last left for unregistered horses.
            Agent horse = ResolveRegisteredHorse(
                riderIdentityScopeId, data.MountData);
            if (horse == null) _dismountedHorses.TryGetValue(agent, out horse);
            if (horse != null && horse.IsActive() && horse.RiderAgent == null)
                agent.MountAgent = horse;
            _dismountedHorses.Remove(agent);
        }
        else if (ownerMounted && agent.HasMount)
        {
            // Owner switched horses (dismount + different re-mount inside one poll interval): the reported
            // mount id no longer matches the horse the puppet sits on — move it over so damage routed by the
            // horse's id keeps hitting what players actually see.
            Agent reported = ResolveRegisteredHorse(
                riderIdentityScopeId, data.MountData);
            if (reported != null && !ReferenceEquals(reported, agent.MountAgent)
                && reported.IsActive() && reported.RiderAgent == null)
            {
                Agent previous = agent.MountAgent;
                _interpolator.Forget(previous);
                agent.MountAgent = reported;
                RestoreLocallyControlledMount(previous);
            }
        }
    }

    // A locally driven rider needs a live horse controller; so does a locally authoritative loose horse.
    // Do not wake a locally owned horse while another controller's active rider is driving it remotely.
    private void EnsureLocallyDrivenMountController(Agent agent)
    {
        Agent mount = agent.IsMount ? agent : agent.MountAgent;
        if (mount == null || !mount.IsActive() || mount.Mission != Mission.Current) return;
        if (mount.RiderAgent is Agent rider && rider.IsActive()
            && !agentRegistry.IsLocallyControlled(rider)) return;
        if (mount.Controller != AgentControllerType.AI)
        {
            mount.SetMaximumSpeedLimit(-1f, isMultiplier: false);
            puppetMountStateRepairer.PrepareForAiControl(mount);
            mount.Controller = AgentControllerType.AI;
        }
    }

    private void RestoreLocallyControlledMount(Agent mount)
    {
        if (mount == null || !mount.IsActive() || mount.Mission != Mission.Current) return;
        if (agentRegistry.TryGetAgentInfo(mount, out _)
            && !agentRegistry.IsLocallyControlled(mount)) return;
        mount.SetMaximumSpeedLimit(-1f, isMultiplier: false);
        if (mount.Controller != AgentControllerType.AI)
        {
            puppetMountStateRepairer.PrepareForAiControl(mount);
            mount.Controller = AgentControllerType.AI;
        }
    }

    /// <summary>
    /// Whether an owned, active, registered agent's movement is broadcast as its OWN packet. Troops always
    /// are. A registered MOUNT is only while it has no live rider: a ridden horse's pose rides in its rider's
    /// MountData (a second stream would fight it), but a masterless one has nothing else driving it — without
    /// its own packets each client's local horse AI wanders its copy and the positions diverge, so its owner
    /// stays authoritative over it the same way it is for a troop. (Public and static so the selection rule is
    /// testable headless; called under the poll's IsActive guard.)
    /// </summary>
    public static bool ShouldBroadcastMovement(Agent agent)
    {
        if (!agent.IsMount) return true;
        return !(agent.RiderAgent is Agent rider && rider.IsActive());
    }

    // The local agent behind a mount's network id; null when the id is empty/unknown or resolves to a
    // non-mount (a stale id after the registry entry was replaced).
    private Agent ResolveRegisteredHorse(
        string riderIdentityScopeId,
        AgentMountData mountData)
    {
        CoopAgentInfo info = null;
        bool found;
        if (mountData.MountMovementId != 0)
        {
            string identityScopeId =
                mountData.MountIdentityScopeId ?? riderIdentityScopeId;
            found = agentRegistry.TryGetAgentInfo(
                identityScopeId, mountData.MountMovementId, out info);
        }
        else
        {
            found = mountData.MountAgentId != Guid.Empty &&
                    agentRegistry.TryGetAgentInfo(mountData.MountAgentId, out info);
        }

        if (!found) return null;
        return info.Agent != null && info.Agent.IsMount ? info.Agent : null;
    }

    private void GetRegisteredMountIdentity(
        Agent agent,
        string riderIdentityScopeId,
        out ushort movementId,
        out string identityScopeId,
        out Guid agentId)
    {
        movementId = 0;
        identityScopeId = null;
        agentId = Guid.Empty;

        var mount = agent.MountAgent;
        if (mount != null && agentRegistry.TryGetAgentInfo(mount, out var mountInfo))
        {
            if (mountInfo.MovementId == 0)
            {
                agentId = mountInfo.AgentId;
                return;
            }

            movementId = mountInfo.MovementId;
            if (mountInfo.MovementScopeId != riderIdentityScopeId)
                identityScopeId = mountInfo.MovementScopeId;
        }
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        // Defensive: if this controller still has a party registered, we missed its earlier departure —
        // clear it so the fresh join re-spawns instead of being deduped as "already registered".
        RemoveControllerParty(payload.What.ControllerId, "peer entered (stale cleanup)");
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        RemoveControllerParty(payload.What.ControllerId, "peer left");
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        RemoveControllerParty(payload.What.ControllerId, "peer disconnected");
    }

    // Despawn and deregister every agent of the controller's party/parties, then drop the parties. A
    // self id is ignored (our own party is managed locally). No-ops when nothing is registered, and is
    // idempotent — so the mesh NetworkLeaveMission path and this server-mediated path can both fire.
    private void RemoveControllerParty(string controllerId, string reason)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        if (controllerId == controllerIdProvider.ControllerId) return;

        // BattleAuthorityMigrator owns battle withdrawal because it can distinguish the player's party from
        // NPC forces the departed host was running. Skip this location-style all-controller cleanup.
        if (BattleSpawnGate.IsCoopBattleActive) return;

        bool sceneActive = Mission.Current != null;

        int removedAgentCount = 0;
        foreach (var agentInfo in agentRegistry.GetAgents(controllerId))
        {
            // Fade the visible agent out only when a scene is still active — a disconnect on leave
            // often arrives mid-teardown with Mission.Current already null.
            if (sceneActive)
            {
                Agent agent = agentInfo.Agent;
                GameThread.RunSafe(() =>
                {
                    if (Mission.Current == null) return;
                    if (agent != null && agent.IsActive() && agent.Health > 0)
                    {
                        bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
                        agent.FadeOut(false, hideMount);
                    }
                });
            }

            agentRegistry.RemoveAgent(agentInfo.Agent);
            removedAgentCount++;
        }
        Logger.Information("[LocationSync] {reason} {ControllerId}: removed {AgentCount} agents (fadedOut={fadedOut})",
            reason, controllerId, removedAgentCount, sceneActive);
    }
}
