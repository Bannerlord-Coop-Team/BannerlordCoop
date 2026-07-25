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

    // --- Delta Movement Thresholding Constants ---
    private const float PositionDeltaThresholdSq = 0.0001f;   // ~1cm squared threshold
    private const float DirectionDeltaThresholdSq = 0.0001f;  // Rotation change threshold
    private const float ForcedSyncIntervalSeconds = 1.0f;     // Heartbeat sync for stationary agents

    private readonly IPacketManager packetManager;
    private readonly IBattleNetwork client;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IAgentEquipmentApplier equipmentApplier;
    private readonly Dictionary<Guid, AgentEquipmentData> lastEquipment = new Dictionary<Guid, AgentEquipmentData>();

    private readonly Dictionary<Agent, Agent> _dismountedHorses = new Dictionary<Agent, Agent>();

    // --- Delta Movement Thresholding Tracking State ---
    private class LastSentMovementState
    {
        public Vec3 Position;
        public Vec2 MovementDirection;
        public float LastSentTime;
    }
    private readonly Dictionary<Guid, LastSentMovementState> _lastSentMovement = new Dictionary<Guid, LastSentMovementState>();
    private float totalSimulationTime = 0f;

    // Per-frame position smoothing for received puppets.
    private readonly AgentPositionInterpolator _interpolator = new AgentPositionInterpolator();
    public IAgentPositionInterpolator Interpolator => _interpolator;

    // Masterless-horse movement receive side.
    private readonly MountMovementApplier _mountMovementApplier;
    public IPacketHandler MountMovementApplier => _mountMovementApplier;

    private bool _disposed;
    private float movementPollElapsed = MovementPollingIntervalSeconds;

    public AgentMovementHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IAgentEquipmentApplier equipmentApplier)
    {
        Logger.Verbose("Creating {handlerType}", typeof(AgentMovementHandler));

        this.packetManager = packetManager;
        this.client = client;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.equipmentApplier = equipmentApplier;

        this.messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        this.messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        this.messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        this.packetManager.RegisterPacketHandler(this);
        this.packetManager.RegisterPacketHandler(equipmentApplier);

        _mountMovementApplier = new MountMovementApplier(agentRegistry, _interpolator);
        this.packetManager.RegisterPacketHandler(_mountMovementApplier);
    }

    ~AgentMovementHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Logger.Verbose("Disposing {handlerType}", typeof(AgentMovementHandler));

        _interpolator.Clear();
        _dismountedHorses.Clear();
        _lastSentMovement.Clear();

        packetManager.RemovePacketHandler(this);
        packetManager.RemovePacketHandler(_mountMovementApplier);
        packetManager.RemovePacketHandler(equipmentApplier);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

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

    // Broadcast every locally authoritative agent using delta thresholding.
    public void PollMovement(float dt)
    {
        if (_disposed || Mission.Current == null) return;

        totalSimulationTime += dt;
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
            if (agent == null || agent.Mission == null || !agent.IsActive()) continue;

            EnsureLocallyDrivenMountController(agent);
            if (!ShouldBroadcastMovement(agent)) continue;

            if (agent.IsMount)
            {
                var mountData = new AgentMountData(agent);

                if (HasMovementExceededThreshold(agentInfo.AgentId, mountData.MountPosition, mountData.MountMovementDirection))
                {
                    AddToBatch(
                        mountGroups,
                        ref legacyMountMovement,
                        agentInfo,
                        mountData);
                }
            }
            else
            {
                GetRegisteredMountIdentity(
                    agent,
                    agentInfo.MovementScopeId,
                    out ushort mountMovementId,
                    out string mountIdentityScopeId,
                    out Guid mountAgentId);

                var agentData = new AgentData(
                    agent,
                    mountMovementId,
                    mountIdentityScopeId,
                    mountAgentId);

                if (HasMovementExceededThreshold(agentInfo.AgentId, agentData.Position, agentData.MovementDirection))
                {
                    AddToBatch(
                        movementGroups,
                        ref legacyMovement,
                        agentInfo,
                        agentData);
                }

                var equipment = new AgentEquipmentData(agent);
                if (!lastEquipment.TryGetValue(agentInfo.AgentId, out var previousEquipment))
                {
                    lastEquipment[agentInfo.AgentId] = equipment;

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

    private bool HasMovementExceededThreshold(Guid agentId, Vec3 currentPos, Vec2 currentDir)
    {
        if (!_lastSentMovement.TryGetValue(agentId, out var lastState))
        {
            _lastSentMovement[agentId] = new LastSentMovementState
            {
                Position = currentPos,
                MovementDirection = currentDir,
                LastSentTime = totalSimulationTime
            };
            return true;
        }

        bool posChanged = (currentPos - lastState.Position).LengthSquared > PositionDeltaThresholdSq;
        bool dirChanged = (currentDir - lastState.MovementDirection).LengthSquared > DirectionDeltaThresholdSq;
        bool heartbeatDue = (totalSimulationTime - lastState.LastSentTime) >= ForcedSyncIntervalSeconds;

        if (posChanged || dirChanged || heartbeatDue)
        {
            lastState.Position = currentPos;
            lastState.MovementDirection = currentDir;
            lastState.LastSentTime = totalSimulationTime;
            return true;
        }

        return false;
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

                    if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
                        continue;

                    if (agentRegistry.IsLocallyControlled(agent))
                        continue;

                    SyncMountState(agent, movement.IdentityScopeId, data);

                    if (agent.MountAgent is Agent puppetMount && puppetMount.Controller != AgentControllerType.None)
                        puppetMount.Controller = AgentControllerType.None;

                    data.Apply(agent);

                    if (agent.HasMount && data.MountData != null)
                    {
                        _interpolator.Forget(agent.MountAgent);
                        _interpolator.SetMountedRiderTarget(
                            agent,
                            data.Position,
                            data.MovementDirection,
                            data.MountData.MountMovementDirection,
                            data.MountData.MountPosition);
                    }
                    else
                    {
                        _interpolator.SetRiderTarget(agent, data.Position, data.MovementDirection);
                    }
                }
            }
        });
    }

    private void SyncMountState(
        Agent agent,
        string riderIdentityScopeId,
        AgentData data)
    {
        bool ownerMounted = data.MountData != null;

        if (!ownerMounted && agent.HasMount)
        {
            Agent horse = agent.MountAgent;
            _dismountedHorses[agent] = horse;
            _interpolator.Forget(horse);
            agent.MountAgent = null;
            RestoreLocallyControlledMount(horse);
        }
        else if (ownerMounted && !agent.HasMount)
        {
            Agent horse = ResolveRegisteredHorse(
                riderIdentityScopeId, data.MountData);

            if (horse == null)
            {
                _dismountedHorses.TryGetValue(agent, out horse);
            }

            if (horse != null && horse.IsActive() && horse.RiderAgent == null)
                agent.MountAgent = horse;

            _dismountedHorses.Remove(agent);
        }
        else if (ownerMounted && agent.HasMount)
        {
            Agent reported = ResolveRegisteredHorse(
                riderIdentityScopeId, data.MountData);

            if (reported != null && !ReferenceEquals(reported, agent.MountAgent)
                && reported.IsActive() && reported.RiderAgent == null)
            {
                Agent previous = agent.MountAgent;
                if (previous != null)
                {
                    _interpolator.Forget(previous);
                }
                agent.MountAgent = reported;
                RestoreLocallyControlledMount(previous);
            }
        }
    }

    private void EnsureLocallyDrivenMountController(Agent agent)
    {
        Agent mount = agent.IsMount ? agent : agent.MountAgent;
        if (mount == null || !mount.IsActive() || mount.Mission != Mission.Current) return;
        if (mount.RiderAgent is Agent rider && rider.IsActive()
            && !agentRegistry.IsLocallyControlled(rider)) return;
        if (mount.Controller != AgentControllerType.AI)
        {
            mount.SetMaximumSpeedLimit(-1f, isMultiplier: false);
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
            mount.Controller = AgentControllerType.AI;
    }

    public static bool ShouldBroadcastMovement(Agent agent)
    {
        if (!agent.IsMount) return true;
        return !(agent.RiderAgent is Agent rider && rider.IsActive());
    }

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

        if (!found || info == null) return null;
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

    private void RemoveControllerParty(string controllerId, string reason)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        if (controllerId == controllerIdProvider.ControllerId) return;

        if (BattleSpawnGate.IsCoopBattleActive) return;

        bool sceneActive = Mission.Current != null;

        int removedAgentCount = 0;
        foreach (var agentInfo in agentRegistry.GetAgents(controllerId))
        {
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

            _lastSentMovement.Remove(agentInfo.AgentId);

            agentRegistry.RemoveAgent(agentInfo.Agent);
            removedAgentCount++;
        }
        Logger.Information("[LocationSync] {reason} {ControllerId}: removed {AgentCount} agents (fadedOut={fadedOut})",
            reason, controllerId, removedAgentCount, sceneActive);
    }
}