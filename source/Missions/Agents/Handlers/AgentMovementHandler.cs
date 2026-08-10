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
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using AgentControllerType = TaleWorlds.Core.AgentControllerType;

namespace Missions.Agents.Handlers;

public interface IAgentMovementHandler : IPacketHandler, IDisposable
{
    /// <summary>
    /// [Game thread] Capture owned agents' continuous movement state and send it to peers.
    /// </summary>
    void PollMovement(float dt);

    MovementRateSnapshot MovementRate { get; }

    void Configure(MovementCadenceProfile profile);

    bool TrySetForcedBulkHz(int? hz, out string error);

    bool TrySetForcedReceiverCapHz(int? hz, out string error);

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

    // Preserve the former 80-poll window at 40 Hz as a cadence-independent two-second animation.
    private const float SyntheticMountTurnDurationSeconds = 2f;
    private const int RemoteSyntheticMountTurnClearGraceFrames = 3;
    private const float UseActionBlendPeriod = -0.2f;

    private const float PositionDeltaThresholdSq = 0.0001f;
    private const float DirectionDeltaThresholdSq = 0.0001f;
    private const float SpeedDeltaThreshold = 0.01f;
    private const float AnimationProgressDeltaThreshold = 0.001f;
    private const float ForcedSyncIntervalSeconds = 0.25f;
    private const float PopulationSampleIntervalSeconds = 1f;
    private const float CadenceEpsilonSeconds = 0.0001f;

    private readonly IPacketManager packetManager;
    private readonly IBattleNetwork client;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IAgentEquipmentApplier equipmentApplier;
    private readonly IMovementBatchSender movementBatchSender;
    private readonly IPuppetMountStateRepairer puppetMountStateRepairer;
    private readonly IAgentVisualActionAccessor visualActionAccessor;
    private readonly IMovementRateController movementRateController;
    private readonly IMissionContext missionContext;
    private readonly Dictionary<Guid, AgentEquipmentData> lastEquipment = new Dictionary<Guid, AgentEquipmentData>();
    // A puppet's horse, remembered when its owner dismounts, so a later re-mount can put it back on the
    // same one. Touched only on the game thread (inside HandlePacket's apply), so no lock; per-mission
    // (this handler is transient), so it can't leak across missions.
    private readonly Dictionary<Agent, Agent> _dismountedHorses = new Dictionary<Agent, Agent>();

    // Vanilla can rotate a stopped AI mount's facing without selecting a turn action.
    private readonly Dictionary<Agent, Vec2> _lastMountDirections = new Dictionary<Agent, Vec2>();
    private readonly Dictionary<Agent, SyntheticMountTurnState> _syntheticMountTurns =
        new Dictionary<Agent, SyntheticMountTurnState>();
    private readonly List<Agent> _completedSyntheticMountTurns =
        new List<Agent>();
    private readonly Dictionary<Agent, RemoteSyntheticMountTurnState> _remoteSyntheticMountTurns =
        new Dictionary<Agent, RemoteSyntheticMountTurnState>();
    private readonly List<Agent> _completedRemoteSyntheticMountTurns =
        new List<Agent>();

    private sealed class SyntheticMountTurnState
    {
        public readonly int Direction;
        public readonly Agent Rider;
        public readonly int ActionIndex;
        public readonly AgentControllerType OriginalController;
        public float ElapsedSeconds;

        public SyntheticMountTurnState(
            int direction,
            Agent rider,
            int actionIndex,
            AgentControllerType originalController)
        {
            Direction = direction;
            Rider = rider;
            ActionIndex = actionIndex;
            OriginalController = originalController;
        }

        public float Progress =>
            Math.Min(0.99f, ElapsedSeconds / SyntheticMountTurnDurationSeconds);
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

    private sealed class LastSentMovementState
    {
        public AgentData AgentData;
        public AgentMountData MountData;
        public bool IsMount;
        public float LastSentTime;
    }

    private sealed class RecipientMovementState
    {
        // A slower recipient must compare against the last absolute snapshot it actually received.
        public readonly Dictionary<Guid, LastSentMovementState> LastSentMovement =
            new Dictionary<Guid, LastSentMovementState>();
        public readonly Dictionary<Guid, float> MovementPendingSince =
            new Dictionary<Guid, float>();
        public bool HasBulkPoll;
        public float LastBulkPollTime;
        public bool SendMountMovementFirst;
    }

    private readonly struct CapturedMovement
    {
        public readonly CoopAgentInfo AgentInfo;
        public readonly AgentData AgentData;
        public readonly AgentMountData MountData;
        public readonly bool IsMount;
        public readonly bool IsPriority;

        public CapturedMovement(
            CoopAgentInfo agentInfo,
            AgentData agentData,
            bool isPriority)
        {
            AgentInfo = agentInfo;
            AgentData = agentData;
            MountData = null;
            IsMount = false;
            IsPriority = isPriority;
        }

        public CapturedMovement(
            CoopAgentInfo agentInfo,
            AgentMountData mountData,
            bool isPriority)
        {
            AgentInfo = agentInfo;
            AgentData = default;
            MountData = mountData;
            IsMount = true;
            IsPriority = isPriority;
        }
    }

    private readonly struct ReceivedMovement
    {
        public readonly string IdentityScopeId;
        public readonly ushort CompactId;
        public readonly Guid CanonicalId;
        public readonly bool UsesCompactId;
        public readonly AgentData Data;
        public readonly AgentMountData MountData;
        public readonly bool IsMount;

        public ReceivedMovement(
            string identityScopeId,
            ushort compactId,
            Guid canonicalId,
            bool usesCompactId,
            AgentData data)
        {
            IdentityScopeId = identityScopeId;
            CompactId = compactId;
            CanonicalId = canonicalId;
            UsesCompactId = usesCompactId;
            Data = data;
            MountData = null;
            IsMount = false;
        }

        public ReceivedMovement(
            string identityScopeId,
            ushort compactId,
            Guid canonicalId,
            bool usesCompactId,
            AgentMountData mountData)
        {
            IdentityScopeId = identityScopeId;
            CompactId = compactId;
            CanonicalId = canonicalId;
            UsesCompactId = usesCompactId;
            Data = default;
            MountData = mountData;
            IsMount = true;
        }
    }

    private readonly Dictionary<string, RecipientMovementState> recipientMovementStates =
        new Dictionary<string, RecipientMovementState>(StringComparer.Ordinal);
    private float totalSimulationTime = 0f;

    // Per-frame position smoothing for received puppets. Fed the latest target on each packet apply (below) and
    // ticked from CoopMissionController.OnMissionTick, so the ease is decoupled from the bursty poll cadence.
    private readonly AgentPositionInterpolator _interpolator;
    public IAgentPositionInterpolator Interpolator => _interpolator;

    // Masterless-horse movement receive side. Owned here (registered/removed with this handler) so both
    // movement streams share one deterministic lifecycle; PollMovement is its send side.
    private readonly MountMovementApplier _mountMovementApplier;
    public IPacketHandler MountMovementApplier => _mountMovementApplier;

    // Dispose is called deterministically on mission teardown (CoopMissionController.OnEndMissionInternal); this
    // guards against a second call (the GC finalizer, or the DI scope also disposing this transient handler).
    private bool _disposed;
    private float bulkPollElapsed;
    private float priorityPollElapsed;
    private float bulkSampleElapsed;
    private float prioritySampleElapsed;
    private float populationSampleElapsed;
    private bool firstMovementPoll = true;
    private bool firstPopulationSample = true;

    public AgentMovementHandler(
        IBattleNetwork client,
        IPacketManager packetManager,
        IMessageBroker messageBroker,
        INetworkAgentRegistry agentRegistry,
        IControllerIdProvider controllerIdProvider,
        IAgentEquipmentApplier equipmentApplier,
        IMovementBatchSender movementBatchSender,
        IPuppetMountStateRepairer puppetMountStateRepairer,
        IAgentVisualActionAccessor visualActionAccessor,
        IMovementRateController movementRateController,
        IMissionContext missionContext)
    {
        if (movementRateController == null) throw new ArgumentNullException(nameof(movementRateController));
        if (missionContext == null) throw new ArgumentNullException(nameof(missionContext));

        Logger.Verbose("Creating {handlerType}", typeof(AgentMovementHandler));

        this.packetManager = packetManager;
        this.client = client;
        this.messageBroker = messageBroker;
        this.agentRegistry = agentRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.equipmentApplier = equipmentApplier;
        this.movementBatchSender = movementBatchSender;
        this.puppetMountStateRepairer = puppetMountStateRepairer;
        this.visualActionAccessor = visualActionAccessor;
        this.movementRateController = movementRateController;
        this.missionContext = missionContext;
        _interpolator = new AgentPositionInterpolator(agentRegistry);
        // Server-mediated membership. A peer entering is the cue to clear any STALE party it left behind
        // on a missed disconnect (so its rejoin re-spawns clean); a leave/disconnect releases its party.
        this.messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        this.messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        this.messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);

        this.packetManager.RegisterPacketHandler(this);
        this.packetManager.RegisterPacketHandler(equipmentApplier);

        _mountMovementApplier = new MountMovementApplier(
            agentRegistry,
            _interpolator,
            puppetMountStateRepairer,
            UpdateRemoteSyntheticMountTurn,
            QueueMountMovement);
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
        _completedSyntheticMountTurns.Clear();
        _remoteSyntheticMountTurns.Clear();
        _completedRemoteSyntheticMountTurns.Clear();

        _dismountedHorses.Clear();
        recipientMovementStates.Clear();

        movementBatchSender.Clear();
        movementRateController.Dispose();

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

    public MovementRateSnapshot MovementRate => movementRateController.Snapshot;

    public void Configure(MovementCadenceProfile profile) =>
        movementRateController.Configure(profile);

    public bool TrySetForcedBulkHz(int? hz, out string error) =>
        movementRateController.TrySetForcedBulkHz(hz, out error);

    public bool TrySetForcedReceiverCapHz(int? hz, out string error) =>
        movementRateController.TrySetForcedReceiverCapHz(hz, out error);

    // Capture every locally authoritative agent once, then apply per-recipient cadence and delta history.
    public void PollMovement(float dt)
    {
        if (_disposed || Mission.Current == null) return;

        movementBatchSender.BeginFrame(dt);
        totalSimulationTime += dt;
        MovementCadence rate = movementRateController.AdvanceFrame(dt);
        float elapsedSeconds = Math.Max(0f, dt);
        bulkPollElapsed += elapsedSeconds;
        priorityPollElapsed += elapsedSeconds;
        bulkSampleElapsed += elapsedSeconds;
        prioritySampleElapsed += elapsedSeconds;
        populationSampleElapsed += elapsedSeconds;

        float bulkInterval = 1f / rate.BulkHz;
        float priorityInterval = 1f / rate.PriorityHz;
        bool authoritativeAgentsDue = firstMovementPoll || bulkPollElapsed >= bulkInterval;
        bool priorityAgentDue = firstMovementPoll || priorityPollElapsed >= priorityInterval;
        if (!authoritativeAgentsDue && !priorityAgentDue) return;

        bool samplePopulation = authoritativeAgentsDue &&
            (firstPopulationSample ||
                populationSampleElapsed >= PopulationSampleIntervalSeconds);

        float elapsedSinceBulkSample = bulkSampleElapsed;
        float elapsedSincePrioritySample = prioritySampleElapsed;
        if (authoritativeAgentsDue)
        {
            bulkPollElapsed = firstMovementPoll ? 0f : bulkPollElapsed % bulkInterval;
            bulkSampleElapsed = 0f;
        }
        if (priorityAgentDue)
        {
            priorityPollElapsed = firstMovementPoll ? 0f : priorityPollElapsed % priorityInterval;
            prioritySampleElapsed = 0f;
        }
        if (samplePopulation)
        {
            populationSampleElapsed = firstPopulationSample
                ? 0f
                : populationSampleElapsed % PopulationSampleIntervalSeconds;
            firstPopulationSample = false;
        }
        firstMovementPoll = false;

        IReadOnlyCollection<CoopAgentInfo> agents = authoritativeAgentsDue
            ? agentRegistry.GetAgents(controllerIdProvider.ControllerId)
            : GetPriorityAgents();
        IReadOnlyCollection<string> recipients = missionContext.ControllersInMission;
        long startedAt = Stopwatch.GetTimestamp();
        MovementTrafficFrame traffic = PollMovementAgents(
            agents,
            recipients,
            authoritativeAgentsDue,
            priorityAgentDue,
            samplePopulation,
            elapsedSinceBulkSample,
            elapsedSincePrioritySample);
        movementRateController.ReportSend(
            ElapsedMilliseconds(startedAt),
            traffic,
            authoritativeAgentsDue);
    }

    private IReadOnlyCollection<CoopAgentInfo> GetPriorityAgents()
    {
        Agent mainAgent = Agent.Main;
        if (mainAgent == null ||
            !agentRegistry.TryGetAgentInfo(mainAgent, out CoopAgentInfo agentInfo) ||
            agentInfo.CurrentAuthority != controllerIdProvider.ControllerId)
        {
            return Array.Empty<CoopAgentInfo>();
        }

        return new[] { agentInfo };
    }

    // Authoritative-agent ticks walk every agent controlled by this peer; priority-only ticks sample Agent.Main.
    private MovementTrafficFrame PollMovementAgents(
        IReadOnlyCollection<CoopAgentInfo> agentInfos,
        IReadOnlyCollection<string> recipients,
        bool includeAuthoritativeAgents,
        bool includePriorityAgent,
        bool samplePopulation,
        float bulkSampleElapsed,
        float prioritySampleElapsed)
    {
        var capturedMovements = new List<CapturedMovement>();
        var equipmentGroups = new Dictionary<string, MovementBatch<AgentEquipmentData>>();
        MovementBatch<AgentEquipmentData> legacyEquipment = null;
        var broadcastAgentIds = new HashSet<Guid>();
        int activeLocallyControlledAgents = 0;

        foreach (CoopAgentInfo agentInfo in agentInfos)
        {
            Agent agent = agentInfo.Agent;
            // Skip agents whose native object is already gone (dead/removed but not yet deregistered):
            // building the snapshot calls into the agent, which can access-violate after native teardown.
            if (agent == null || agent.Mission == null || !agent.IsActive()) continue;
            if (samplePopulation)
                activeLocallyControlledAgents++;

            EnsureLocallyDrivenMountController(agent);
            if (!ShouldBroadcastMovement(agent)) continue;
            bool isPriority = ReferenceEquals(agent, Agent.Main);
            if (includeAuthoritativeAgents)
                broadcastAgentIds.Add(agentInfo.AgentId);
            if ((isPriority && !includePriorityAgent) ||
                (!isPriority && !includeAuthoritativeAgents))
                continue;
            float sampleElapsed = isPriority
                ? prioritySampleElapsed
                : bulkSampleElapsed;

            if (agent.IsMount)
            {
                int turnDirection = EnsureStationaryMountTurnAnimation(
                    agent,
                    sampleElapsed,
                    out int turnActionIndex,
                    out float turnProgress,
                    out bool syntheticTurn);
                var mountData = new AgentMountData(
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
                    mountAction0IsSyntheticTurn: syntheticTurn);

                capturedMovements.Add(
                    new CapturedMovement(agentInfo, mountData, isPriority));
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
                    sampleElapsed,
                    out int turnActionIndex,
                    out float turnProgress,
                    out bool syntheticTurn);
                var agentData = new AgentData(
                    agent,
                    mountMovementId,
                    mountIdentityScopeId,
                    mountAgentId,
                    turnDirection == AgentMountData.NoTurn ? (int?)null : turnDirection,
                    turnDirection == AgentMountData.NoTurn ? (int?)null : turnActionIndex,
                    turnDirection == AgentMountData.NoTurn ? (float?)null : turnProgress,
                    mountAction0IsSyntheticTurn: syntheticTurn);

                capturedMovements.Add(
                    new CapturedMovement(agentInfo, agentData, isPriority));

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

        if (samplePopulation)
            movementRateController.ReportPopulation(
                CountActiveMissionAgents(),
                activeLocallyControlledAgents);
        if (includeAuthoritativeAgents)
            RemoveStaleLocalState(broadcastAgentIds);
        SendEquipment(equipmentGroups.Values);
        SendEquipment(legacyEquipment);

        var traffic = new MovementTrafficFrame();
        foreach (string recipientId in recipients)
        {
            if (string.IsNullOrEmpty(recipientId) ||
                recipientId == controllerIdProvider.ControllerId)
                continue;

            RecipientMovementState recipient = GetOrCreateRecipientState(recipientId);
            bool recipientBulkDue = includeAuthoritativeAgents &&
                IsRecipientBulkDue(
                    recipient,
                    movementRateController.GetReceiverCapHz(recipientId));
            if (!recipientBulkDue && !includePriorityAgent) continue;

            traffic = traffic.Add(SendMovementToRecipient(
                recipientId,
                recipient,
                capturedMovements,
                recipientBulkDue,
                includePriorityAgent));
        }

        return traffic;
    }

    private RecipientMovementState GetOrCreateRecipientState(string controllerId)
    {
        if (recipientMovementStates.TryGetValue(
                controllerId,
                out RecipientMovementState recipient))
        {
            return recipient;
        }

        recipient = new RecipientMovementState();
        recipientMovementStates.Add(controllerId, recipient);
        return recipient;
    }

    private bool IsRecipientBulkDue(
        RecipientMovementState recipient,
        int receiverCapHz)
    {
        float interval = 1f / Math.Max(1, receiverCapHz);
        if (recipient.HasBulkPoll &&
            totalSimulationTime - recipient.LastBulkPollTime + CadenceEpsilonSeconds < interval)
        {
            return false;
        }

        if (recipient.HasBulkPoll)
        {
            float elapsed = totalSimulationTime - recipient.LastBulkPollTime;
            int elapsedIntervals = Math.Max(
                1,
                (int)Math.Floor((elapsed + CadenceEpsilonSeconds) / interval));
            recipient.LastBulkPollTime += elapsedIntervals * interval;
        }
        else
        {
            recipient.LastBulkPollTime = totalSimulationTime;
        }
        recipient.HasBulkPoll = true;
        return true;
    }

    private MovementTrafficFrame SendMovementToRecipient(
        string controllerId,
        RecipientMovementState recipient,
        IReadOnlyCollection<CapturedMovement> capturedMovements,
        bool includeAuthoritativeAgents,
        bool includePriorityAgent)
    {
        var movementGroups = new Dictionary<string, MovementBatch<AgentData>>();
        var priorityMovementGroups = new Dictionary<string, MovementBatch<AgentData>>();
        var mountGroups = new Dictionary<string, MovementBatch<AgentMountData>>();
        MovementBatch<AgentData> legacyMovement = null;
        MovementBatch<AgentData> legacyPriorityMovement = null;
        MovementBatch<AgentMountData> legacyMountMovement = null;

        foreach (CapturedMovement captured in capturedMovements)
        {
            if ((captured.IsPriority && !includePriorityAgent) ||
                (!captured.IsPriority && !includeAuthoritativeAgents))
            {
                continue;
            }

            Guid agentId = captured.AgentInfo.AgentId;
            if (captured.IsMount)
            {
                if (!ShouldSendMovement(recipient, agentId, captured.MountData))
                    continue;

                MarkMovementPending(recipient, agentId);
                AddToBatch(
                    mountGroups,
                    ref legacyMountMovement,
                    captured.AgentInfo,
                    captured.MountData);
                continue;
            }

            if (!ShouldSendMovement(recipient, agentId, captured.AgentData))
                continue;

            MarkMovementPending(recipient, agentId);
            if (captured.IsPriority)
            {
                AddToBatch(
                    priorityMovementGroups,
                    ref legacyPriorityMovement,
                    captured.AgentInfo,
                    captured.AgentData,
                    isPriority: true);
            }
            else
            {
                AddToBatch(
                    movementGroups,
                    ref legacyMovement,
                    captured.AgentInfo,
                    captured.AgentData);
            }
        }

        int maxPayloadBytes = client.GetMaxUnreliablePayloadBytes(controllerId);
        // The local player's current input/position gets first access to each refill. Formation AI then uses
        // the rotating cursor, so responsiveness stays high without letting early registry entries monopolize it.
        MovementSendResult priorityResult = movementBatchSender.Send(
            controllerId,
            priorityMovementGroups.Values,
            legacyPriorityMovement,
            maxPayloadBytes,
            CreateMovementPacket,
            (agentId, data) => RecordSentMovement(recipient, agentId, data));
        MovementSendResult movementResult;
        MovementSendResult mountResult;
        if (recipient.SendMountMovementFirst)
        {
            mountResult = movementBatchSender.Send(
                controllerId,
                mountGroups.Values,
                legacyMountMovement,
                maxPayloadBytes,
                CreateMountMovementPacket,
                (agentId, data) => RecordSentMovement(recipient, agentId, data));
            movementResult = movementBatchSender.Send(
                controllerId,
                movementGroups.Values,
                legacyMovement,
                maxPayloadBytes,
                CreateMovementPacket,
                (agentId, data) => RecordSentMovement(recipient, agentId, data));
        }
        else
        {
            movementResult = movementBatchSender.Send(
                controllerId,
                movementGroups.Values,
                legacyMovement,
                maxPayloadBytes,
                CreateMovementPacket,
                (agentId, data) => RecordSentMovement(recipient, agentId, data));
            mountResult = movementBatchSender.Send(
                controllerId,
                mountGroups.Values,
                legacyMountMovement,
                maxPayloadBytes,
                CreateMountMovementPacket,
                (agentId, data) => RecordSentMovement(recipient, agentId, data));
        }
        recipient.SendMountMovementFirst = !recipient.SendMountMovementFirst;

        return movementBatchSender.EndFrame(
            controllerId,
            priorityResult.DeferredCount +
                movementResult.DeferredCount +
                mountResult.DeferredCount,
            GetMaximumDeferredAge(recipient));
    }

    private static int CountActiveMissionAgents()
    {
        int count = 0;
        foreach (Agent agent in Mission.Current.Agents)
        {
            if (agent != null && agent.IsActive()) count++;
        }
        return count;
    }

    private static double ElapsedMilliseconds(long startedAt) =>
        (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;

    private bool ShouldSendMovement(
        RecipientMovementState recipient,
        Guid agentId,
        AgentData current)
    {
        if (!recipient.LastSentMovement.TryGetValue(agentId, out var lastState))
            return true;

        if (lastState.IsMount ||
            HasMovementChanged(lastState.AgentData, current) ||
            IsHeartbeatDue(lastState))
            return true;

        return false;
    }

    private bool ShouldSendMovement(
        RecipientMovementState recipient,
        Guid agentId,
        AgentMountData current)
    {
        if (!recipient.LastSentMovement.TryGetValue(agentId, out var lastState))
            return true;

        if (!lastState.IsMount ||
            HasMountMovementChanged(lastState.MountData, current) ||
            IsHeartbeatDue(lastState))
            return true;

        return false;
    }

    private void RecordSentMovement(
        RecipientMovementState recipient,
        Guid agentId,
        AgentData current)
    {
        recipient.LastSentMovement[agentId] = new LastSentMovementState
        {
            AgentData = current,
            MountData = null,
            IsMount = false,
            LastSentTime = totalSimulationTime
        };
        recipient.MovementPendingSince.Remove(agentId);
    }

    private void RecordSentMovement(
        RecipientMovementState recipient,
        Guid agentId,
        AgentMountData current)
    {
        recipient.LastSentMovement[agentId] = new LastSentMovementState
        {
            AgentData = default,
            MountData = current,
            IsMount = true,
            LastSentTime = totalSimulationTime
        };
        recipient.MovementPendingSince.Remove(agentId);
    }

    private void MarkMovementPending(
        RecipientMovementState recipient,
        Guid agentId)
    {
        if (!recipient.MovementPendingSince.ContainsKey(agentId))
            recipient.MovementPendingSince[agentId] = totalSimulationTime;
    }

    private float GetMaximumDeferredAge(RecipientMovementState recipient)
    {
        float maximumAge = 0f;
        foreach (float pendingSince in recipient.MovementPendingSince.Values)
            maximumAge = Math.Max(maximumAge, totalSimulationTime - pendingSince);
        return maximumAge;
    }

    private bool IsHeartbeatDue(LastSentMovementState state)
    {
        return totalSimulationTime - state.LastSentTime >= ForcedSyncIntervalSeconds;
    }

    private static bool HasMovementChanged(AgentData previous, AgentData current)
    {
        return (current.Position - previous.Position).LengthSquared > PositionDeltaThresholdSq ||
               (current.MovementDirection - previous.MovementDirection).LengthSquared > DirectionDeltaThresholdSq ||
               (current.InputVector - previous.InputVector).LengthSquared > DirectionDeltaThresholdSq ||
               (current.LookDirection - previous.LookDirection).LengthSquared > DirectionDeltaThresholdSq ||
               Math.Abs(current.Speed - previous.Speed) > SpeedDeltaThreshold ||
               HasMountMovementChanged(previous.MountData, current.MountData);
    }

    private static bool HasMountMovementChanged(AgentMountData previous, AgentMountData current)
    {
        if (previous == null || current == null)
            return previous != current;

        return (current.MountPosition - previous.MountPosition).LengthSquared > PositionDeltaThresholdSq ||
               (current.MountMovementDirection - previous.MountMovementDirection).LengthSquared > DirectionDeltaThresholdSq ||
               (current.MountInputVector - previous.MountInputVector).LengthSquared > DirectionDeltaThresholdSq ||
               (current.MountLookDirection - previous.MountLookDirection).LengthSquared > DirectionDeltaThresholdSq ||
               Math.Abs(current.MountSpeed - previous.MountSpeed) > SpeedDeltaThreshold ||
               current.MountAction0Index != previous.MountAction0Index ||
               current.MountAction0Flag != previous.MountAction0Flag ||
               current.MountAction0TurnDirection != previous.MountAction0TurnDirection ||
               current.MountAction0TurnActionIndex != previous.MountAction0TurnActionIndex ||
               current.MountAction0IsSyntheticTurn != previous.MountAction0IsSyntheticTurn ||
               (current.MountAction0IsSyntheticTurn &&
                   Math.Abs(current.MountAction0Progress - previous.MountAction0Progress) >
                       AnimationProgressDeltaThreshold) ||
               current.MountAction1Index != previous.MountAction1Index ||
               current.MountAction1Flag != previous.MountAction1Flag ||
               current.MountMovementId != previous.MountMovementId ||
               current.MountAgentId != previous.MountAgentId ||
               !string.Equals(
                   current.MountIdentityScopeId,
                   previous.MountIdentityScopeId,
                   StringComparison.Ordinal);
    }

    private void RemoveStaleLocalState(HashSet<Guid> broadcastAgentIds)
    {
        foreach (RecipientMovementState recipient in recipientMovementStates.Values)
        {
            var staleRecipientAgentIds = new HashSet<Guid>();
            foreach (Guid agentId in recipient.LastSentMovement.Keys)
            {
                if (!broadcastAgentIds.Contains(agentId))
                    staleRecipientAgentIds.Add(agentId);
            }
            foreach (Guid agentId in recipient.MovementPendingSince.Keys)
            {
                if (!broadcastAgentIds.Contains(agentId))
                    staleRecipientAgentIds.Add(agentId);
            }

            foreach (Guid agentId in staleRecipientAgentIds)
            {
                recipient.LastSentMovement.Remove(agentId);
                recipient.MovementPendingSince.Remove(agentId);
            }
        }

        var staleEquipmentAgentIds = new List<Guid>();
        foreach (Guid agentId in lastEquipment.Keys)
        {
            if (!broadcastAgentIds.Contains(agentId))
                staleEquipmentAgentIds.Add(agentId);
        }
        foreach (Guid agentId in staleEquipmentAgentIds)
            lastEquipment.Remove(agentId);
    }

    public void ReplaySyntheticMountTurnAnimationsAfterNativeTick()
    {
        _completedSyntheticMountTurns.Clear();
        foreach (var pair in _syntheticMountTurns)
        {
            Agent mount = pair.Key;
            SyntheticMountTurnState syntheticTurn = pair.Value;
            bool stillLocallyControlled =
                syntheticTurn.Rider == null
                    ? mount != null
                        && mount.RiderAgent == null
                        && agentRegistry.IsLocallyControlled(mount)
                    : mount != null
                        && ReferenceEquals(
                            mount.RiderAgent,
                            syntheticTurn.Rider)
                        && agentRegistry.IsLocallyControlled(
                            syntheticTurn.Rider);
            if (mount == null
                || mount.Mission == null
                || !mount.IsActive()
                || !stillLocallyControlled)
            {
                _completedSyntheticMountTurns.Add(mount);
                continue;
            }

            visualActionAccessor.TrySetAction(
                mount,
                0,
                new ActionIndexCache(syntheticTurn.ActionIndex),
                syntheticTurn.Progress,
                UseActionBlendPeriod);
        }
        foreach (Agent mount in _completedSyntheticMountTurns)
            ClearSyntheticMountTurn(mount);

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

            Agent rider = mount.RiderAgent;
            if (agentRegistry.IsLocallyControlled(mount)
                || (rider != null
                    && agentRegistry.IsLocallyControlled(rider)))
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
            && mountData.MountAction0IsSyntheticTurn
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
            if (!AgentActionData.TryResolveActionIndex(
                    mountData.MountAction0TurnActionIndex,
                    out ActionIndexCache turnAction))
            {
                if (activeTurn != null)
                    activeTurn.ClearRequested = true;
                return;
            }

            _remoteSyntheticMountTurns[mount] =
                new RemoteSyntheticMountTurnState(
                    mountData.MountAction0TurnDirection,
                    turnAction.Index,
                    mountData.MountAction0Progress);
            return;
        }

        if (activeTurn.ClearRequested)
            activeTurn.Progress = mountData.MountAction0Progress;
        else if (mountData.MountAction0Progress > activeTurn.Progress)
            activeTurn.Progress = mountData.MountAction0Progress;
        activeTurn.FramesSinceUpdate = 0;
        activeTurn.ClearRequested = false;
    }

    private int EnsureStationaryMountTurnAnimation(
        Agent mount,
        float elapsedSeconds,
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
        if (_syntheticMountTurns.TryGetValue(
                mount,
                out SyntheticMountTurnState pendingTurn)
            && HasMovementIntent(mount, pendingTurn.Rider))
        {
            ClearSyntheticMountTurn(mount);
            _lastMountDirections[mount] = currentDirection;
            return AgentMountData.NoTurn;
        }
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

            activeSyntheticTurn.ElapsedSeconds += Math.Max(0f, elapsedSeconds);
            if (activeSyntheticTurn.ElapsedSeconds + 0.0001f
                >= SyntheticMountTurnDurationSeconds)
            {
                ClearSyntheticMountTurn(mount);
                _lastMountDirections[mount] = currentDirection;
                return AgentMountData.NoTurn;
            }

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
        AgentControllerType originalController = mount.Controller;
        if (originalController != AgentControllerType.None)
            mount.Controller = AgentControllerType.None;
        _syntheticMountTurns[mount] =
            new SyntheticMountTurnState(
                turnDirection,
                rider,
                actionIndex,
                originalController);
    }

    private static bool HasMovementIntent(Agent mount, Agent rider)
    {
        return mount.MovementInputVector.LengthSquared > 0.0001f
            || (rider != null
                && rider.IsActive()
                && rider.MovementInputVector.LengthSquared > 0.0001f);
    }

    private void ClearSyntheticMountTurn(Agent mount)
    {
        if (!_syntheticMountTurns.TryGetValue(
                mount,
                out SyntheticMountTurnState syntheticTurn))
            return;

        _syntheticMountTurns.Remove(mount);
        if (mount != null
            && mount.IsActive()
            && mount.Controller == AgentControllerType.None
            && syntheticTurn.OriginalController != AgentControllerType.None
            && (syntheticTurn.Rider == null
                ? agentRegistry.IsLocallyControlled(mount)
                : ReferenceEquals(mount.RiderAgent, syntheticTurn.Rider)
                    && agentRegistry.IsLocallyControlled(syntheticTurn.Rider)))
        {
            mount.Controller = syntheticTurn.OriginalController;
        }
    }

    private static void AddToBatch<T>(
        Dictionary<string, MovementBatch<T>> compactBatches,
        ref MovementBatch<T> legacyBatch,
        CoopAgentInfo agentInfo,
        T data,
        bool isPriority = false)
    {
        MovementBatch<T> batch;
        if (agentInfo.MovementId == 0)
        {
            batch = legacyBatch ??= new MovementBatch<T>(null, isPriority);
        }
        else if (!compactBatches.TryGetValue(agentInfo.MovementScopeId, out batch))
        {
            batch = new MovementBatch<T>(agentInfo.MovementScopeId, isPriority);
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

    private static IPacket CreateMovementPacket(
        string identityScopeId,
        ushort[] compactIds,
        Guid[] canonicalIds,
        AgentData[] data)
    {
        return identityScopeId == null
            ? new MovementPacket(canonicalIds, data)
            : new MovementPacket(identityScopeId, compactIds, data);
    }

    private static IPacket CreateMountMovementPacket(
        string identityScopeId,
        ushort[] compactIds,
        Guid[] canonicalIds,
        AgentMountData[] data)
    {
        return identityScopeId == null
            ? new MountMovementPacket(canonicalIds, data)
            : new MountMovementPacket(identityScopeId, compactIds, data);
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

        if (_disposed) return;

        bool usesCompactIds = movement.AgentIds != null;
        var snapshots = new ReceivedMovement[idCount];
        for (int i = 0; i < idCount; i++)
        {
            snapshots[i] = new ReceivedMovement(
                movement.IdentityScopeId,
                usesCompactIds ? movement.AgentIds[i] : (ushort)0,
                usesCompactIds ? Guid.Empty : movement.AgentGuids[i],
                usesCompactIds,
                movement.Agents[i]);
        }

        QueueReceivedMovement(snapshots);
    }

    private void QueueMountMovement(MountMovementPacket movement)
    {
        int idCount = movement.MountIds?.Length ?? movement.MountGuids?.Length ?? 0;
        if (idCount == 0 || movement.Mounts == null || movement.Mounts.Length != idCount || _disposed)
            return;

        bool usesCompactIds = movement.MountIds != null;
        var snapshots = new ReceivedMovement[idCount];
        for (int i = 0; i < idCount; i++)
        {
            snapshots[i] = new ReceivedMovement(
                movement.IdentityScopeId,
                usesCompactIds ? movement.MountIds[i] : (ushort)0,
                usesCompactIds ? Guid.Empty : movement.MountGuids[i],
                usesCompactIds,
                movement.Mounts[i]);
        }

        QueueReceivedMovement(snapshots);
    }

    private void QueueReceivedMovement(ReceivedMovement[] snapshots)
    {
        long queuedAt = Stopwatch.GetTimestamp();
        // Resolve and apply each received packet in one game-thread action so it remains FIFO-ordered
        // with spawn, deployment, and authority work queued by earlier messages.
        GameThread.RunSafe(
            () =>
            {
                double queueMilliseconds = ElapsedMilliseconds(queuedAt);
                long applyStartedAt = Stopwatch.GetTimestamp();
                try
                {
                    ApplyMovement(snapshots);
                }
                finally
                {
                    movementRateController.ReportReceive(
                        queueMilliseconds,
                        ElapsedMilliseconds(applyStartedAt),
                        snapshots.Length);
                }
            },
            context: nameof(HandlePacket));
    }

    private void ApplyMovement(IReadOnlyList<ReceivedMovement> snapshots)
    {
        if (_disposed || Mission.Current == null) return;

        using (new AllowedThread())
        {
            foreach (ReceivedMovement snapshot in snapshots)
            {
                if (snapshot.IsMount)
                {
                    _mountMovementApplier.ApplySnapshot(
                        snapshot.IdentityScopeId,
                        snapshot.CompactId,
                        snapshot.CanonicalId,
                        snapshot.UsesCompactId,
                        snapshot.MountData);
                    continue;
                }

                CoopAgentInfo agentInfo;
                bool found = snapshot.UsesCompactId
                    ? agentRegistry.TryGetAgentInfo(
                        snapshot.IdentityScopeId, snapshot.CompactId, out agentInfo)
                    : agentRegistry.TryGetAgentInfo(snapshot.CanonicalId, out agentInfo);
                if (!found) continue;

                Agent agent = agentInfo.Agent;
                AgentData data = snapshot.Data;

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
                SyncMountState(
                    agent,
                    snapshot.IdentityScopeId ?? agentInfo.MovementScopeId,
                    data);
                if (previousMount != null && !ReferenceEquals(previousMount, agent.MountAgent))
                    UpdateRemoteSyntheticMountTurn(previousMount, null);

                // A puppet horse must not run local AI between owner snapshots and fight their heading/input.
                if (agent.MountAgent is Agent puppetMount && puppetMount.Controller != AgentControllerType.None)
                {
                    puppetMount.Controller = AgentControllerType.None;
                    puppetMountStateRepairer.PreserveRiderlessPuppet(puppetMount);
                }

                data.Apply(agent);
                if (data.MountData != null && agent.MountAgent is Agent remoteMount)
                    UpdateRemoteSyntheticMountTurn(remoteMount, data.MountData);

                // Position is reconciled per-frame by the interpolator (smoother than a per-packet
                // correction bound to the ~10ms poll cadence); push the latest targets it eases toward.
                if (agent.HasMount && data.MountData != null)
                {
                    _interpolator.Forget(agent.MountAgent);
                    _interpolator.SetMountedRiderTarget(agent, data);
                }
                else
                {
                    _interpolator.SetRiderTarget(agent, data);
                }
            }
        }
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
                if (previous != null)
                {
                    _interpolator.Forget(previous);
                }
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
        if (_syntheticMountTurns.ContainsKey(mount)) return;
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
        RemoveRecipientMovementState(payload.What.ControllerId);
        // Defensive: if this controller still has a party registered, we missed its earlier departure —
        // clear it so the fresh join re-spawns instead of being deduped as "already registered".
        RemoveControllerParty(payload.What.ControllerId, "peer entered (stale cleanup)");
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        RemoveRecipientMovementState(payload.What.ControllerId);
        RemoveControllerParty(payload.What.ControllerId, "peer left");
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        RemoveRecipientMovementState(payload.What.ControllerId);
        RemoveControllerParty(payload.What.ControllerId, "peer disconnected");
    }

    private void RemoveRecipientMovementState(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        GameThread.RunSafe(() =>
        {
            if (_disposed) return;
            recipientMovementStates.Remove(controllerId);
            movementBatchSender.RemoveRecipient(controllerId);
        });
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
