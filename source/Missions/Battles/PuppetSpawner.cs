using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Data;
using GameInterface.Services.Players;
using Missions.Messages;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

#if DEBUG
public sealed class RejoinResultTransitionDebugState
{
    public bool MissionResultPresent { get; }
    public bool BattleResolved { get; }
    public bool PlayerVictory { get; }
    public string BattleState { get; }

    public RejoinResultTransitionDebugState(Mission mission)
    {
        var result = mission?.MissionResult;
        MissionResultPresent = result != null;
        BattleResolved = result?.BattleResolved ?? false;
        PlayerVictory = result?.PlayerVictory ?? false;
        BattleState = result?.BattleState.ToString();
    }
}

public sealed class ReturningHeroCatchUpDebugRecord
{
    public string Phase { get; }
    public string BattleInstanceId { get; }
    public string LocalControllerId { get; }
    public string AgentId { get; }
    public string OwnerControllerId { get; }
    public string OriginalOwnerControllerId { get; }
    public string CharacterId { get; }
    public string MapEventPartyId { get; }
    public int TroopSeed { get; }
    public string Side { get; }
    public string SpawnPurpose { get; }
    public int ProcessId { get; }
    public bool CurrentOwnerIsLocal { get; }
    public bool OriginalOwnerIsLocal { get; }
    public bool ReturningHeroIdentityMatched { get; }
    public string ReturningHeroIdentityError { get; }
    public bool DeploymentCommitted { get; }
    public bool DeploymentControllerPresent { get; }
    public bool TeamSetupOver { get; }
    public bool DeploymentBlocked { get; }
    public string DeploymentBlockReason { get; }
    public int PendingRecordCount { get; }
    public int MatchingPendingRecordCount { get; }
    public CoopTroopSupplierDebugObservation SupplierObservation { get; }
    public float MissionTime { get; }
    public DateTime CapturedUtc { get; }
    public RejoinResultTransitionDebugState ResultTransition { get; }

    public ReturningHeroCatchUpDebugRecord(
        string phase,
        string battleInstanceId,
        string localControllerId,
        BattleAgentSpawnData data,
        SpawnBatchPurpose spawnPurpose,
        int processId,
        bool currentOwnerIsLocal,
        bool originalOwnerIsLocal,
        bool returningHeroIdentityMatched,
        string returningHeroIdentityError,
        bool deploymentCommitted,
        bool deploymentControllerPresent,
        bool teamSetupOver,
        bool deploymentBlocked,
        string deploymentBlockReason,
        int pendingRecordCount,
        int matchingPendingRecordCount,
        CoopTroopSupplierDebugObservation supplierObservation,
        float missionTime,
        DateTime capturedUtc,
        RejoinResultTransitionDebugState resultTransition)
    {
        Phase = phase;
        BattleInstanceId = battleInstanceId;
        LocalControllerId = localControllerId;
        AgentId = data.AgentId.ToString("D");
        OwnerControllerId = data.OwnerControllerId;
        OriginalOwnerControllerId = data.OriginalOwnerControllerId;
        CharacterId = data.CharacterId;
        MapEventPartyId = data.MapEventPartyId;
        TroopSeed = data.TroopSeed;
        Side = data.Side.ToString();
        SpawnPurpose = spawnPurpose.ToString();
        ProcessId = processId;
        CurrentOwnerIsLocal = currentOwnerIsLocal;
        OriginalOwnerIsLocal = originalOwnerIsLocal;
        ReturningHeroIdentityMatched = returningHeroIdentityMatched;
        ReturningHeroIdentityError = returningHeroIdentityError;
        DeploymentCommitted = deploymentCommitted;
        DeploymentControllerPresent = deploymentControllerPresent;
        TeamSetupOver = teamSetupOver;
        DeploymentBlocked = deploymentBlocked;
        DeploymentBlockReason = deploymentBlockReason;
        PendingRecordCount = pendingRecordCount;
        MatchingPendingRecordCount = matchingPendingRecordCount;
        SupplierObservation = supplierObservation;
        MissionTime = missionTime;
        CapturedUtc = capturedUtc;
        ResultTransition = resultTransition;
    }
}

public sealed class ReturningHeroCatchUpDebugError
{
    public string Phase { get; }
    public int ProcessId { get; }
    public string BattleInstanceId { get; }
    public string LocalControllerId { get; }
    public string AgentId { get; }
    public string OwnerControllerId { get; }
    public string OriginalOwnerControllerId { get; }
    public string SpawnPurpose { get; }
    public float MissionTime { get; }
    public string Error { get; }
    public DateTime CapturedUtc { get; }

    public ReturningHeroCatchUpDebugError(
        string phase,
        int processId,
        string battleInstanceId,
        string localControllerId,
        BattleAgentSpawnData data,
        SpawnBatchPurpose spawnPurpose,
        float missionTime,
        string error,
        DateTime capturedUtc)
    {
        Phase = phase;
        ProcessId = processId;
        BattleInstanceId = battleInstanceId;
        LocalControllerId = localControllerId;
        AgentId = data == null ? null : data.AgentId.ToString("D");
        OwnerControllerId = data == null ? null : data.OwnerControllerId;
        OriginalOwnerControllerId = data == null ? null : data.OriginalOwnerControllerId;
        SpawnPurpose = spawnPurpose.ToString();
        MissionTime = missionTime;
        Error = error;
        CapturedUtc = capturedUtc;
    }
}

public sealed class ReturningHeroCatchUpDebugState
{
    public string BattleInstanceId { get; }
    public string RequestedControllerId { get; }
    public string LocalControllerId { get; }
    public bool RequestedControllerIsLocal { get; }
    public bool ReturningPlayerIdentityAvailable { get; }
    public string ReturningHeroCharacterId { get; }
    public string ReturningPlayerIdentityError { get; }
    public ReturningHeroCatchUpDebugRecord FirstCatchUpBeforeDefer { get; }
    public ReturningHeroCatchUpDebugRecord AfterPendingInsertion { get; }
    public ReturningHeroCatchUpDebugError[] DiagnosticErrors { get; }

    public ReturningHeroCatchUpDebugState(
        string battleInstanceId,
        string requestedControllerId,
        string localControllerId,
        bool requestedControllerIsLocal,
        bool returningPlayerIdentityAvailable,
        string returningHeroCharacterId,
        string returningPlayerIdentityError,
        ReturningHeroCatchUpDebugRecord firstCatchUpBeforeDefer,
        ReturningHeroCatchUpDebugRecord afterPendingInsertion,
        ReturningHeroCatchUpDebugError[] diagnosticErrors)
    {
        BattleInstanceId = battleInstanceId;
        RequestedControllerId = requestedControllerId;
        LocalControllerId = localControllerId;
        RequestedControllerIsLocal = requestedControllerIsLocal;
        ReturningPlayerIdentityAvailable = returningPlayerIdentityAvailable;
        ReturningHeroCharacterId = returningHeroCharacterId;
        ReturningPlayerIdentityError = returningPlayerIdentityError;
        FirstCatchUpBeforeDefer = firstCatchUpBeforeDefer;
        AfterPendingInsertion = afterPendingInsertion;
        DiagnosticErrors = diagnosticErrors;
    }
}

public sealed class PendingPuppetDebugRecord
{
    public string AgentId { get; set; }
    public string OwnerControllerId { get; set; }
    public string OriginalOwnerControllerId { get; set; }
    public string CharacterId { get; set; }
    public string MapEventPartyId { get; set; }
    public int TroopSeed { get; set; }
    public string Side { get; set; }
    public bool MatchesRequestedOwner { get; set; }
    public bool MatchesRequestedHero { get; set; }
    public bool IsOwnAgent { get; set; }
    public bool DeploymentBlocked { get; set; }
    public string DeploymentBlockReason { get; set; }
}

public sealed class PendingPuppetDebugState
{
    public string BattleInstanceId { get; set; }
    public string RequestedControllerId { get; set; }
    public bool MissionPresent { get; set; }
    public bool DeploymentCommitted { get; set; }
    public bool DeploymentControllerPresent { get; set; }
    public bool TeamSetupOver { get; set; }
    public int PendingRecordCount { get; set; }
    public int MatchingRecordCount { get; set; }
    public bool ReturningHeroRecordPresent { get; set; }
    public List<PendingPuppetDebugRecord> MatchingRecords { get; } = new List<PendingPuppetDebugRecord>();
}
#endif

/// <summary>
/// Peer-side spawn application for a coop battle: spawns the agents other owners replicate over the mesh
/// (<see cref="NetworkSpawnBattleAgents"/>) as local puppets driven by their owner's movement. Spawns that
/// arrive before their team or explicit party identity exists are buffered and drained on tick. During local
/// deployment setup, remote records wait until native team setup completes; this client's own withheld records
/// wait for commit.
/// </summary>
public interface IPuppetSpawner : IDisposable
{
    /// <summary>
    /// [Game thread] Drain puppets whose teams and party identities now exist, retaining local records until commit.
    /// </summary>
    void DrainPendingPuppets();
    bool HasRetainedPlayerAgent(Agent agent);

#if DEBUG
    /// <summary>[Game thread] Copy pending records for one returning player without changing their queue.</summary>
    PendingPuppetDebugState CapturePendingPuppetState(string controllerId);

    /// <summary>[Game thread] Copies the first local returning-hero catch-up records without changing their queue.</summary>
    ReturningHeroCatchUpDebugState CaptureReturningHeroCatchUpState(string controllerId);
#endif
}

/// <inheritdoc cref="IPuppetSpawner"/>
public class PuppetSpawner : IPuppetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetSpawner>();
    private const int MaxBufferedSpawnsPerTick = 64;

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;
    private readonly IBattleAgentBudget agentBudget;
    private readonly IMissionWeaponDataMapper missionWeaponDataMapper;
    private readonly IBattleAgentSpawnBatchCodec spawnBatchCodec;
    private readonly IPuppetRoutApplier puppetRoutApplier;
    private readonly IBattleAuthorityMigrator authorityMigrator;

    // Spawn records can arrive before their mission team or world-stream party. Buffer them until both exist;
    // agents without that identity later break team ownership and scoreboard attribution.
    private readonly object pendingPuppetLock = new object();
    private readonly List<BattleAgentSpawnData> pendingPuppets = new List<BattleAgentSpawnData>();
    private readonly Dictionary<Guid, NetworkRetainedPlayerHero> playerHandoffs = new();
    private readonly HashSet<Guid> appliedPlayerHandoffs = new();
#if DEBUG
    private readonly object returningHeroCatchUpLock = new object();
    private ReturningHeroCatchUpDebugRecord firstReturningHeroCatchUpBeforeDefer;
    private ReturningHeroCatchUpDebugRecord returningHeroCatchUpAfterPendingInsertion;
    private readonly List<ReturningHeroCatchUpDebugError> returningHeroCatchUpErrors =
        new List<ReturningHeroCatchUpDebugError>();
#endif
    private readonly object withdrawnControllerLock = new object();
    private readonly HashSet<string> withdrawnControllers = new HashSet<string>();
    private readonly HashSet<string> withdrawnHostControllers = new HashSet<string>();
    private readonly HashSet<Guid> retainedFormerHostAgentIds = new HashSet<Guid>();

    public PuppetSpawner(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner,
        IBattleAgentBudget agentBudget,
        IMissionWeaponDataMapper missionWeaponDataMapper,
        IPuppetRoutApplier puppetRoutApplier = null,
        IBattleAgentSpawnBatchCodec spawnBatchCodec = null,
        IBattleAuthorityMigrator authorityMigrator = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;
        this.agentBudget = agentBudget;
        this.missionWeaponDataMapper = missionWeaponDataMapper;
        this.spawnBatchCodec = spawnBatchCodec ?? new BattleAgentSpawnBatchCodec();
        this.puppetRoutApplier = puppetRoutApplier;
        this.authorityMigrator = authorityMigrator;

        messageBroker.Subscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
        messageBroker.Subscribe<NetworkRetainedPlayerHero>(Handle_RetainedPlayerHero);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSpawnBattleAgents>(Handle_NetworkSpawnBattleAgents);
        messageBroker.Unsubscribe<NetworkRetainedPlayerHero>(Handle_RetainedPlayerHero);
        playerHandoffs.Clear();
        appliedPlayerHandoffs.Clear();
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    private void Handle_RetainedPlayerHero(MessagePayload<NetworkRetainedPlayerHero> payload)
    {
        var handoff = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current?.GetMissionBehavior<CoopBattleController>()?.Session != session
                || handoff == null || !handoff.HasValidAuthorityTransition
                || handoff.BattleInstanceId != session.InstanceId
                || authorityMigrator?.IsCurrentPlayerHandoff(handoff) != true) return;
            if (playerHandoffs.TryGetValue(handoff.Previous.AgentId, out var previous)
                && previous.Previous.AuthorityRevision >= handoff.Previous.AuthorityRevision) return;
            playerHandoffs[handoff.Previous.AgentId] = handoff;
            appliedPlayerHandoffs.Remove(handoff.Previous.AgentId);
            ApplyPendingPlayerHandoffs();
        }, context: nameof(Handle_RetainedPlayerHero));
    }

    private void ApplyPendingPlayerHandoffs()
    {
        foreach (var pair in playerHandoffs)
        {
            if (appliedPlayerHandoffs.Contains(pair.Key) || authorityMigrator == null
                || !authorityMigrator.IsPlayerHandoffIdentityValid(pair.Value)) continue;
            var registry = coopMissionComponent.AgentRegistry;
            if (registry.TryGetAgentInfo(pair.Key, out _))
            {
                if (authorityMigrator.ApplyGrantedPlayerHandoff(pair.Value))
                    appliedPlayerHandoffs.Add(pair.Key);
                continue;
            }
            var returned = pair.Value.CreateReturnedRecord();
            lock (pendingPuppetLock)
            {
                pendingPuppets.RemoveAll(data => data.AgentId == pair.Key);
                pendingPuppets.Insert(0, returned);
            }
            appliedPlayerHandoffs.Add(pair.Key);
        }
    }

    public bool HasRetainedPlayerAgent(Agent agent)
    {
        if (agent == null || !agent.IsActive() || agent.Mission != Mission.Current
            || agent.Character != Hero.MainHero?.CharacterObject
            || !coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info)
            || !session.IsOwn(info.CurrentAuthority)
            || !playerHandoffs.TryGetValue(info.AgentId, out var handoff)) return false;
        return session.IsOwn(handoff.ReturningControllerId)
            && authorityMigrator?.IsPlayerHandoffIdentityValid(handoff) == true
            && info.AuthorityRevision == handoff.Previous.AuthorityRevision + 1
            && Mission.Current.GetMissionBehavior<CoopBattleMissionSpawnHandler>()
                ?.HasSuppliedPlayerOrigin(handoff.Previous) == true;
    }

    private bool IsRetainedPlayerBootstrap(BattleAgentSpawnData data)
    {
        if (!session.IsOwn(data.OwnerControllerId)
            || !playerHandoffs.TryGetValue(data.AgentId, out var handoff)
            || !appliedPlayerHandoffs.Contains(data.AgentId)
            || handoff.ReturningControllerId != data.OwnerControllerId
            || data.AuthorityRevision != handoff.Previous.AuthorityRevision + 1
            || authorityMigrator?.IsPlayerHandoffIdentityValid(handoff) != true
            || Mission.Current.InitialPlayerAgent != null || Mission.Current.MainAgent != null) return false;
        var spawnHandler = Mission.Current.GetMissionBehavior<CoopBattleMissionSpawnHandler>();
        return spawnHandler?.HasSuppliedPlayerOrigin(data) == true;
    }

    private void Handle_NetworkSpawnBattleAgents(MessagePayload<NetworkSpawnBattleAgents> payload)
    {
        NetworkSpawnBattleAgents message = payload.What;
        if (!spawnBatchCodec.TryDecode(message, out BattleAgentSpawnData[] agents))
        {
            Logger.Error(
                "[BattleTraffic] Rejected malformed spawn batch {TransferId} {BatchIndex}/{BatchCount} " +
                "with {RecordCount} declared record(s)",
                message.TransferId,
                message.BatchIndex + 1,
                message.BatchCount,
                message.RecordCount);
            return;
        }

        Logger.Information(
            "[BattleTraffic] Received {Count} {Purpose} spawn record(s), batch {BatchIndex}/{BatchCount}, " +
            "{WireBytes} wire bytes from {UncompressedBytes} protobuf bytes",
            agents.Length,
            message.Purpose,
            message.BatchIndex + 1,
            Math.Max(1, message.BatchCount),
            message.Payload?.Length ?? 0,
            message.UncompressedLength);

        // One bounded game-thread action per wire batch preserves ReliableOrdered barriers without adding one
        // queue entry per agent. The codec caps production batches at 32 records.
        GameThread.RunSafe(
            () => SpawnPuppetBatch(message, agents),
            context: nameof(Handle_NetworkSpawnBattleAgents));
    }

    private void SpawnPuppetBatch(
        NetworkSpawnBattleAgents message,
        BattleAgentSpawnData[] agents)
    {
        if (Mission.Current == null)
        {
            Logger.Warning(
                "[BattleTraffic] Dropping spawn transfer {TransferId} batch {BatchIndex}/{BatchCount}: mission ended",
                message.TransferId,
                message.BatchIndex + 1,
                Math.Max(1, message.BatchCount));
            return;
        }

        int slotsAvailable = agentBudget.RemainingCapacity(agentBudget.CountLiveAgents(Mission.Current));
        foreach (BattleAgentSpawnData data in PlayerHeroesFirst(agents))
        {
            if (data == null || data.AgentId == Guid.Empty) continue;

            try
            {
#if DEBUG
                bool isReturningHeroCatchUp = TryIdentifyReturningHeroCatchUpRecord(
                    data,
                    message.Purpose);
                if (isReturningHeroCatchUp)
                    TryRecordFirstReturningHeroCatchUpBeforeDefer(data, message.Purpose);
#endif
                if (!TrySpawnPuppetNow(data, ref slotsAvailable))
                {
                    lock (pendingPuppetLock)
                    {
                        pendingPuppets.Add(data);
                    }
#if DEBUG
                    if (isReturningHeroCatchUp)
                        TryRecordReturningHeroCatchUpAfterPendingInsertion(data, message.Purpose);
#endif
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "[BattleSync] Failed to spawn puppet {AgentId}; dropping it", data.AgentId);
            }
        }

        Logger.Information(
            "[BattleTraffic] Applied spawn transfer {TransferId} batch {BatchIndex}/{BatchCount} on the game thread",
            message.TransferId,
            message.BatchIndex + 1,
            Math.Max(1, message.BatchCount));
    }

    // [Game thread] Spawn one puppet, consuming <paramref name="slotsAvailable"/> render slots on success.
    // Returns false when a required team, explicit party identity, deployment state, or render slot is pending.
    private bool TrySpawnPuppetNow(BattleAgentSpawnData data, ref int slotsAvailable)
    {
        var registry = coopMissionComponent.AgentRegistry;

        if (Mission.Current == null) return true;                       // no mission — drop
        if (IsWithdrawnPlayerParty(data)) return true;                  // stale replay after leave/drop — drop
        if (registry.TryGetAgentInfo(data.AgentId, out _)) return true; // already spawned — dedupe
        bool isRetainedFormerHostRecord = IsRetainedFormerHostRecord(data);

        if (playerHandoffs.TryGetValue(data.AgentId, out var handoff))
        {
            if (!appliedPlayerHandoffs.Contains(data.AgentId)
                || authorityMigrator?.IsPlayerHandoffIdentityValid(handoff) != true) return false;
            if (data.AuthorityRevision <= handoff.Previous.AuthorityRevision)
                data = handoff.CreateReturnedRecord();
        }
        bool isOwnAgent = session.IsOwn(data.OwnerControllerId);
        bool retainedPlayerBootstrap = IsRetainedPlayerBootstrap(data);
        if (LocalDeploymentBlocksSpawn(isOwnAgent) && !retainedPlayerBootstrap) return false;

        // BR-110: the engine renders at most a fixed number of agents. At capacity the puppet is deferred, not
        // dropped — buffered and retried by DrainPendingPuppets as removals free slots. A mounted record spawns
        // rider AND horse in one SpawnAgent call, so it needs two slots; whether it mounts is read from the
        // SPAWN EQUIPMENT (the horse the engine mints), not MountAgentId — a catch-up record can carry an empty
        // MountAgentId (the original horse already died) while its equipment still spawns a fresh mount. The
        // caller owns the running slot budget so a drain counts capacity once instead of per buffered puppet.
        int slotsNeeded = agentBudget.SlotsForEquipment(data.SpawnEquipment);
        if (slotsNeeded > slotsAvailable) return false; // at capacity — buffer

        var team = ResolvePuppetTeam(data);
        if (team == null) return false;                                 // teams not created yet — buffer

        if (!objectManager.TryGetObjectWithLogging(data.CharacterId, out CharacterObject character))
        {
            Logger.Warning("[BattleSync] Puppet skipped: unresolved character {Char} for agent {AgentId}", data.CharacterId, data.AgentId);
            return true;
        }

        // We own the agent when the record's assignment owner is us. This can be our initial spawn record or a
        // fresh record after re-entry. Our hero becomes the local main agent; our troops become locally driven
        // AI combatants. Everything else is an inert puppet driven by its owner over the mesh.
        bool isOwnHero = isOwnAgent && character.IsHero && character.HeroObject == Hero.MainHero;

        // Carry the troop's party so the agent has a real BattleCombatant — the battle observer/scoreboard
        // reads origin.BattleCombatant, and SimpleAgentOrigin leaves it null for non-hero troops.
        var party = ResolvePuppetParty(data.MapEventPartyId);

        if (party == null)
        {
            // World-state registration and mission-mesh spawns use different channels. An explicit party id
            // can therefore arrive first; retain it until the game-thread registry apply catches up.
            if (!string.IsNullOrEmpty(data.MapEventPartyId)) return false;

            // An unattributed spawn record must still produce a body — a puppet that never spawns is an
            // invisible enemy (and re-buffering forever spams the log every tick). Fall back to any
            // involved party on the agent's side; only the scoreboard attribution degrades.
            party = ResolveFallbackParty(data.Side);
            if (party == null)
            {
                Logger.Warning("[BattleSync] Puppet skipped: unresolved party {Party} for agent {AgentId}", data.MapEventPartyId, data.AgentId);
                return false;
            }

            Logger.Warning("[BattleSync] Puppet {AgentId} spawned with a fallback {Side} party; {Party} unresolved", data.AgentId, data.Side, data.MapEventPartyId);
        }

        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(data.TroopSeed));

        var missionEquipment = ResolveMissionEquipment(data.MissionEquipmentData);

        var buildData = new AgentBuildData(character);
        buildData.InitialPosition(data.Position);
        buildData.Team(team);
        buildData.InitialDirection(Vec2.Forward);
        buildData.Equipment(data.SpawnEquipment); // Use calculated equipment from spawning client instead of character equipment (random per troop per client)
        buildData.BodyProperties(data.BodyProperties);
        buildData.Banner(origin.Banner);
        buildData.TroopOrigin(origin);
        buildData.MissionEquipment(missionEquipment);
        buildData.Controller(isOwnHero ? AgentControllerType.Player
            : isOwnAgent ? AgentControllerType.AI
            : AgentControllerType.None);
        buildData.ClothingColor1(origin.FactionColor);
        buildData.ClothingColor2(origin.FactionColor2);

        // Suppress capture for the duration of this spawn: a puppet is another owner's troop replicated to us,
        // not ours, so BattleAgentSpawnedPatch must not re-capture and re-broadcast it.
        Agent agent;
        BattleSpawnGate.SuppressCapture = true;
        try
        {
            using (new TransientEquipmentSyncScope())
            {
                agent = Mission.Current.SpawnAgent(buildData);
            }
        }
        finally
        {
            BattleSpawnGate.SuppressCapture = false;
        }
        agent.FadeIn();
        if (data.Health > 0) agent.Health = data.Health;

        formationAssigner.Assign(agent, data.FormationIndex);
        if (retainedPlayerBootstrap)
        {
            // BuildAgent has established InitialPlayerAgent; native deployment still owns its activation.
            agent.Controller = AgentControllerType.None;
            agent.SetIsAIPaused(true);
        }

        // Adopt our own hero as the controllable main agent of this mission.
        if (isOwnHero)
        {
            Mission.Current.MainAgent = agent;
        }
        else if (isOwnAgent)
        {
            // One of our own troops arriving as a spawn record. It spawned AI-controlled and locally driven
            // (never an interpolated puppet — we are its authority the moment it registers below), so wake its
            // AI exactly as the adopt/reinforcement paths do or it stands idle. If our
            // hero died while we were gone, the leaderless-control path (ChargeLeaderlessOwnTroops) charges
            // these formations at our deployment finish, exactly as it does for a fresh leaderless spawn.
            AgentAiWaker.Wake(agent);
        }
        else
        {
            // Keep the puppet un-paused so it follows its owner's movement even while THIS client is still in its
            // own deployment freeze (native deployment sets Mission.AllowAiTicking=false and AI-pauses agents). A
            // mid-battle joiner spawns these puppets while deploying into an ALREADY-LIVE battle; left paused, the
            // puppet never walks the small per-tick deltas its owner sends (AgentData.Apply only teleports on >1u
            // jumps), so the whole live battle looks frozen until the joiner clicks Start Battle. Mirrors the
            // adopt and reinforcement paths, which un-pause too.
            agent.SetIsAIPaused(false);
        }

        bool agentRegistered = registry.TryRegisterAgent(
            data.OwnerControllerId,
            data.OriginalOwnerControllerId,
            data.MovementScopeId,
            data.AgentId,
            data.MovementId,
            agent,
            data.AuthorityRevision);
        if (!agentRegistered)
        {
            Logger.Error(
                "[BattleDesync] Spawned puppet remained unregistered: kind=rider agentId={AgentId} " +
                "owner={Owner} originalOwner={OriginalOwner} movementIdentity={Scope}/{MovementId} " +
                "agentIndex={AgentIndex} character={CharacterId} ownAgent={OwnAgent}",
                data.AgentId,
                data.OwnerControllerId,
                data.OriginalOwnerControllerId,
                data.MovementScopeId,
                data.MovementId,
                agent.Index,
                data.CharacterId,
                isOwnAgent);
        }
        if (data.IsRunningAway)
            puppetRoutApplier?.ApplyFleeing(agent);
        if (data.HasCurrentEquipment)
            data.CurrentEquipment.Apply(agent);

        // The owner registered its cavalry's horse with its own network id; our engine spawned a matching
        // horse implicitly (same equipment) inside SpawnAgent. Register OUR copy under the same id, so mount
        // hits route by the horse's identity and its death broadcast finds it. No casualty record — a horse
        // is not a roster troop. If the puppet unexpectedly spawned on foot, the id just stays unmapped here
        // and hits on the (nonexistent) horse can't occur anyway.
        if (data.MountAgentId != Guid.Empty)
        {
            if (agent.MountAgent is Agent mount)
            {
                bool mountRegistered = registry.TryRegisterAgent(
                    data.OwnerControllerId,
                    data.MountOriginalOwnerControllerId,
                    data.MountMovementScopeId,
                    data.MountAgentId,
                    data.MountMovementId,
                    mount,
                    data.MountAuthorityRevision);
                if (!mountRegistered)
                {
                    Logger.Error(
                        "[BattleDesync] Spawned puppet remained unregistered: kind=mount agentId={AgentId} " +
                        "riderAgentId={RiderAgentId} owner={Owner} originalOwner={OriginalOwner} " +
                        "movementIdentity={Scope}/{MovementId} agentIndex={AgentIndex} character={CharacterId}",
                        data.MountAgentId,
                        data.AgentId,
                        data.OwnerControllerId,
                        data.MountOriginalOwnerControllerId,
                        data.MountMovementScopeId,
                        data.MountMovementId,
                        mount.Index,
                        data.CharacterId);
                }
            }
            else
                Logger.Warning("[BattleSync] Spawn record for {AgentId} carries mount {MountId} but the puppet spawned unmounted", data.AgentId, data.MountAgentId);
        }

        // A retained record of a departed host can drain after the migration sweep. Every peer moves
        // the late registry entries to the current host; only that host revives the rider as battle AI.
        if (isRetainedFormerHostRecord && agentRegistered && handoff == null)
        {
            authorityMigrator?.ApplyLateSpawnedPuppet(
                agent,
                data.AgentId,
                agent.MountAgent,
                data.MountAgentId);
        }

        // Key the casualty on the troop's CHARACTER through the object manager (never a raw StringId).
        objectManager.TryGetId(character, out var troopCharacterId);
        casualties.Record(data.AgentId, data.MapEventPartyId, data.TroopSeed, troopCharacterId);
        Logger.Information("[BattleSync] Spawned puppet {Char} (agent {AgentId}, ownAgent={Own})", data.CharacterId, data.AgentId, isOwnAgent);
        slotsAvailable -= slotsNeeded;
        return true;
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        GameThread.RunSafe(() =>
        {
            lock (withdrawnControllerLock)
            {
                // Re-entry clears current withdrawal, but former-host lineage stays for retained records
                // that can still drain after the controller returns.
                withdrawnControllers.Remove(payload.What.ControllerId);
            }
        });
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        MarkControllerWithdrawn(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        MarkControllerWithdrawn(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void MarkControllerWithdrawn(string controllerId, string instanceId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (instanceId != null && instanceId != session.InstanceId) return;
        bool wasHost = session.IsHostController(controllerId);
        lock (withdrawnControllerLock)
        {
            withdrawnControllers.Add(controllerId);
            if (wasHost) withdrawnHostControllers.Add(controllerId);
        }

        // Records received before the departure may already be sitting in the deployment buffer. Remove the
        // player's party on the game thread, while leaving NPC parties from the old host available to migrate.
        GameThread.RunSafe(() =>
        {
            var withdrawnHandoffs = new List<Guid>();
            foreach (var pair in playerHandoffs)
                if (pair.Value.ReturningControllerId == controllerId)
                    withdrawnHandoffs.Add(pair.Key);
            foreach (var agentId in withdrawnHandoffs)
            {
                playerHandoffs.Remove(agentId);
                appliedPlayerHandoffs.Remove(agentId);
            }

            var retainedAgentIds = new List<Guid>();
            lock (pendingPuppetLock)
            {
                pendingPuppets.RemoveAll(data =>
                {
                    if (data.OwnerControllerId != controllerId) return false;
                    bool remove = !wasHost || IsPlayerPartyRecord(data, controllerId);
                    if (!remove) retainedAgentIds.Add(data.AgentId);
                    return remove;
                });
            }

            if (retainedAgentIds.Count > 0)
            {
                lock (withdrawnControllerLock)
                    retainedFormerHostAgentIds.UnionWith(retainedAgentIds);
            }
        });
    }

    // [Game thread] A replay from the old host can already be buffered when its disconnect arrives. Drop only
    // records for that player's own party; NPC parties the host ran still belong to the successor migration.
    private bool IsWithdrawnPlayerParty(BattleAgentSpawnData data)
    {
        bool wasHost;
        lock (withdrawnControllerLock)
        {
            if (!withdrawnControllers.Contains(data.OwnerControllerId)) return false;
            wasHost = withdrawnHostControllers.Contains(data.OwnerControllerId);
        }

        return !wasHost || IsPlayerPartyRecord(data, data.OwnerControllerId);
    }

    private bool IsRetainedFormerHostRecord(BattleAgentSpawnData data)
    {
        bool isDepartedHost;
        lock (withdrawnControllerLock)
        {
            if (retainedFormerHostAgentIds.Contains(data.AgentId)) return true;
            isDepartedHost = withdrawnControllers.Contains(data.OwnerControllerId)
                && withdrawnHostControllers.Contains(data.OwnerControllerId);
        }

        if (!isDepartedHost || IsPlayerPartyRecord(data, data.OwnerControllerId)) return false;

        lock (withdrawnControllerLock)
            retainedFormerHostAgentIds.Add(data.AgentId);
        return true;
    }

    // [Game thread] Match a spawn record to the controller's player party. The hero check covers a record
    // whose MapEventParty id was unavailable when it was captured.
    private bool IsPlayerPartyRecord(BattleAgentSpawnData data, string controllerId)
    {
        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;

        if (data.MapEventPartyId != null
            && objectManager.TryGetObject<MapEventParty>(data.MapEventPartyId, out var mapEventParty)
            && objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty)
            && mapEventParty?.Party == mobileParty?.Party)
        {
            return true;
        }

        return objectManager.TryGetObject<Hero>(player.HeroId, out var hero)
            && objectManager.TryGetObject<CharacterObject>(data.CharacterId, out var character)
            && character.IsHero
            && character.HeroObject == hero;
    }

    private bool LocalDeploymentBlocksSpawn(bool isOwnAgent)
    {
        if (deployment.IsCommitted)
            return false;

        var controller = Mission.Current?.GetMissionBehavior<DeploymentMissionController>();
        return controller != null && (isOwnAgent || !controller.TeamSetupOver);
    }

#if DEBUG
    public PendingPuppetDebugState CapturePendingPuppetState(string controllerId)
    {
        Mission mission = Mission.Current;
        DeploymentMissionController deploymentController = mission?.GetMissionBehavior<DeploymentMissionController>();
        string returningCharacterId = null;
        if (playerManager.TryGetPlayer(controllerId, out var returningPlayer))
            returningCharacterId = returningPlayer.CharacterObjectId;

        var state = new PendingPuppetDebugState
        {
            BattleInstanceId = session.InstanceId,
            RequestedControllerId = controllerId,
            MissionPresent = mission != null,
            DeploymentCommitted = deployment.IsCommitted,
            DeploymentControllerPresent = deploymentController != null,
            TeamSetupOver = deploymentController?.TeamSetupOver ?? false,
        };

        lock (pendingPuppetLock)
        {
            state.PendingRecordCount = pendingPuppets.Count;
            foreach (BattleAgentSpawnData data in pendingPuppets)
            {
                bool matchesRequestedOwner = string.Equals(
                    data.OwnerControllerId,
                    controllerId,
                    StringComparison.Ordinal);
                bool matchesRequestedHero = !string.IsNullOrEmpty(returningCharacterId)
                    && string.Equals(data.CharacterId, returningCharacterId, StringComparison.Ordinal);
                if (!matchesRequestedOwner && !matchesRequestedHero)
                    continue;

                bool isOwnAgent = session.IsOwn(data.OwnerControllerId);
                bool deploymentBlocked = !deployment.IsCommitted
                    && deploymentController != null
                    && (isOwnAgent || !deploymentController.TeamSetupOver);
                state.MatchingRecords.Add(new PendingPuppetDebugRecord
                {
                    AgentId = data.AgentId.ToString("D"),
                    OwnerControllerId = data.OwnerControllerId,
                    OriginalOwnerControllerId = data.OriginalOwnerControllerId,
                    CharacterId = data.CharacterId,
                    MapEventPartyId = data.MapEventPartyId,
                    TroopSeed = data.TroopSeed,
                    Side = data.Side.ToString(),
                    MatchesRequestedOwner = matchesRequestedOwner,
                    MatchesRequestedHero = matchesRequestedHero,
                    IsOwnAgent = isOwnAgent,
                    DeploymentBlocked = deploymentBlocked,
                    DeploymentBlockReason = DescribeDeploymentBlock(
                        mission,
                        deploymentController,
                        isOwnAgent,
                        deploymentBlocked),
                });
                if (matchesRequestedHero)
                    state.ReturningHeroRecordPresent = true;
            }
        }

        state.MatchingRecords.Sort((left, right) => string.CompareOrdinal(left.AgentId, right.AgentId));
        state.MatchingRecordCount = state.MatchingRecords.Count;
        return state;
    }

    public ReturningHeroCatchUpDebugState CaptureReturningHeroCatchUpState(string controllerId)
    {
        bool requestedControllerIsLocal = string.Equals(
            controllerId,
            session.OwnControllerId,
            StringComparison.Ordinal);
        bool returningPlayerIdentityAvailable = false;
        string returningHeroCharacterId = null;
        string returningPlayerIdentityError = null;
        if (requestedControllerIsLocal)
        {
            try
            {
                if (playerManager.TryGetPlayer(controllerId, out var returningPlayer))
                {
                    returningPlayerIdentityAvailable = true;
                    returningHeroCharacterId = returningPlayer.CharacterObjectId;
                    if (string.IsNullOrWhiteSpace(returningHeroCharacterId))
                    {
                        returningPlayerIdentityAvailable = false;
                        returningPlayerIdentityError = "returning-player-character-unavailable";
                    }
                }
                else
                {
                    returningPlayerIdentityError = "returning-player-unregistered";
                }
            }
            catch (Exception e)
            {
                returningPlayerIdentityError = "returning-player-identity-read-failed: " + e.GetType().Name;
            }
        }
        else
        {
            returningPlayerIdentityError = "requested-controller-is-not-local";
        }

        ReturningHeroCatchUpDebugRecord beforeDefer;
        ReturningHeroCatchUpDebugRecord afterPendingInsertion;
        ReturningHeroCatchUpDebugError[] diagnosticErrors;
        lock (returningHeroCatchUpLock)
        {
            beforeDefer = firstReturningHeroCatchUpBeforeDefer;
            afterPendingInsertion = returningHeroCatchUpAfterPendingInsertion;
            diagnosticErrors = returningHeroCatchUpErrors.ToArray();
        }

        return new ReturningHeroCatchUpDebugState(
            session.InstanceId,
            controllerId,
            session.OwnControllerId,
            requestedControllerIsLocal,
            returningPlayerIdentityAvailable,
            returningHeroCharacterId,
            returningPlayerIdentityError,
            beforeDefer,
            afterPendingInsertion,
            diagnosticErrors);
    }

    private bool TryIdentifyReturningHeroCatchUpRecord(BattleAgentSpawnData data, SpawnBatchPurpose purpose)
    {
        if (purpose != SpawnBatchPurpose.CatchUp)
            return false;

        try
        {
            bool currentOwnerIsLocal = session.IsOwn(data.OwnerControllerId);
            bool originalOwnerIsLocal = session.IsOwn(data.OriginalOwnerControllerId);
            if (!playerManager.TryGetPlayer(session.OwnControllerId, out var returningPlayer))
            {
                if (currentOwnerIsLocal || originalOwnerIsLocal)
                {
                    RecordReturningHeroCatchUpDiagnosticError(
                        "returning-hero-identity-unavailable",
                        data,
                        purpose,
                        "local-returning-player-unregistered");
                }
                return false;
            }

            if (string.IsNullOrWhiteSpace(returningPlayer.CharacterObjectId))
            {
                if (currentOwnerIsLocal || originalOwnerIsLocal)
                {
                    RecordReturningHeroCatchUpDiagnosticError(
                        "returning-hero-identity-unavailable",
                        data,
                        purpose,
                        "local-returning-player-character-unavailable");
                }
                return false;
            }

            if (!string.Equals(data.CharacterId, returningPlayer.CharacterObjectId, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            RecordReturningHeroCatchUpDiagnosticError(
                "returning-hero-identity-resolution-failed",
                data,
                purpose,
                e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    private void TryRecordFirstReturningHeroCatchUpBeforeDefer(
        BattleAgentSpawnData data,
        SpawnBatchPurpose purpose)
    {
        try
        {
            ReturningHeroCatchUpDebugRecord captured = CaptureReturningHeroCatchUpRecord(
                "first-returning-hero-catch-up-before-defer",
                data,
                purpose,
                pendingRecordCount: -1,
                matchingPendingRecordCount: -1);
            bool retained = false;
            lock (returningHeroCatchUpLock)
            {
                if (firstReturningHeroCatchUpBeforeDefer == null)
                {
                    firstReturningHeroCatchUpBeforeDefer = captured;
                    retained = true;
                }
            }

            if (retained)
                LogReturningHeroCatchUpCapture(captured);
        }
        catch (Exception e)
        {
            RecordReturningHeroCatchUpDiagnosticError(
                "first-returning-hero-catch-up-before-defer-capture-failed",
                data,
                purpose,
                e.GetType().Name + ": " + e.Message);
        }
    }

    private void TryRecordReturningHeroCatchUpAfterPendingInsertion(
        BattleAgentSpawnData data,
        SpawnBatchPurpose purpose)
    {
        try
        {
            int pendingRecordCount;
            int matchingPendingRecordCount = 0;
            lock (pendingPuppetLock)
            {
                pendingRecordCount = pendingPuppets.Count;
                foreach (BattleAgentSpawnData pending in pendingPuppets)
                {
                    if (string.Equals(pending.OwnerControllerId, data.OwnerControllerId, StringComparison.Ordinal) &&
                        string.Equals(pending.OriginalOwnerControllerId, data.OriginalOwnerControllerId, StringComparison.Ordinal) &&
                        string.Equals(pending.CharacterId, data.CharacterId, StringComparison.Ordinal))
                    {
                        matchingPendingRecordCount++;
                    }
                }
            }

            ReturningHeroCatchUpDebugRecord captured = CaptureReturningHeroCatchUpRecord(
                "returning-hero-catch-up-after-pending-insertion",
                data,
                purpose,
                pendingRecordCount,
                matchingPendingRecordCount);
            bool retained = false;
            lock (returningHeroCatchUpLock)
            {
                if (returningHeroCatchUpAfterPendingInsertion == null)
                {
                    returningHeroCatchUpAfterPendingInsertion = captured;
                    retained = true;
                }
            }

            if (retained)
                LogReturningHeroCatchUpCapture(captured);
        }
        catch (Exception e)
        {
            RecordReturningHeroCatchUpDiagnosticError(
                "returning-hero-catch-up-after-pending-insertion-capture-failed",
                data,
                purpose,
                e.GetType().Name + ": " + e.Message);
        }
    }

    private ReturningHeroCatchUpDebugRecord CaptureReturningHeroCatchUpRecord(
        string phase,
        BattleAgentSpawnData data,
        SpawnBatchPurpose purpose,
        int pendingRecordCount,
        int matchingPendingRecordCount)
    {
        Mission mission = Mission.Current;
        DeploymentMissionController deploymentController = mission?.GetMissionBehavior<DeploymentMissionController>();
        bool currentOwnerIsLocal = session.IsOwn(data.OwnerControllerId);
        bool originalOwnerIsLocal = session.IsOwn(data.OriginalOwnerControllerId);
        bool deploymentBlocked = !deployment.IsCommitted && deploymentController != null &&
            (currentOwnerIsLocal || !deploymentController.TeamSetupOver);
        CoopTroopSupplierDebugObservation supplierObservation = CoopTroopSupplierRegistry.CaptureDebugObservation(
            session.InstanceId,
            data.MapEventPartyId);
        return new ReturningHeroCatchUpDebugRecord(
            phase,
            session.InstanceId,
            session.OwnControllerId,
            data,
            purpose,
            Process.GetCurrentProcess().Id,
            currentOwnerIsLocal,
            originalOwnerIsLocal,
            true,
            null,
            deployment.IsCommitted,
            deploymentController != null,
            deploymentController?.TeamSetupOver ?? false,
            deploymentBlocked,
            DescribeDeploymentBlock(mission, deploymentController, currentOwnerIsLocal, deploymentBlocked),
            pendingRecordCount,
            matchingPendingRecordCount,
            supplierObservation,
            mission?.CurrentTime ?? -1f,
            DateTime.UtcNow,
            new RejoinResultTransitionDebugState(mission));
    }

    private static void LogReturningHeroCatchUpCapture(ReturningHeroCatchUpDebugRecord captured)
    {
        if (captured == null) return;

        try
        {
            Logger.Information(
                "[BattleSync] DEBUG returning-hero catch-up observation-json {ObservationJson}",
                JsonConvert.SerializeObject(captured));
        }
        catch (Exception e)
        {
            LogReturningHeroCatchUpDiagnosticFailure(
                e,
                "[BattleSync] Failed to emit returning-hero catch-up observation");
        }
    }

    private void RecordReturningHeroCatchUpDiagnosticError(
        string phase,
        BattleAgentSpawnData data,
        SpawnBatchPurpose purpose,
        string error)
    {
        try
        {
            Mission mission = Mission.Current;
            var captured = new ReturningHeroCatchUpDebugError(
                phase,
                Process.GetCurrentProcess().Id,
                session.InstanceId,
                session.OwnControllerId,
                data,
                purpose,
                mission?.CurrentTime ?? -1f,
                error,
                DateTime.UtcNow);
            bool retained = false;
            lock (returningHeroCatchUpLock)
            {
                if (returningHeroCatchUpErrors.Count < 8)
                {
                    returningHeroCatchUpErrors.Add(captured);
                    retained = true;
                }
            }

            if (retained)
                LogReturningHeroCatchUpDiagnosticError(captured);
        }
        catch (Exception e)
        {
            LogReturningHeroCatchUpDiagnosticFailure(
                e,
                "[BattleSync] Failed to record returning-hero catch-up diagnostic error");
        }
    }

    private static void LogReturningHeroCatchUpDiagnosticError(ReturningHeroCatchUpDebugError captured)
    {
        try
        {
            Logger.Warning(
                "[BattleSync] DEBUG returning-hero catch-up diagnostic-error-json {ErrorJson}",
                JsonConvert.SerializeObject(captured));
        }
        catch (Exception e)
        {
            LogReturningHeroCatchUpDiagnosticFailure(
                e,
                "[BattleSync] Failed to emit returning-hero catch-up diagnostic error");
        }
    }

    private static void LogReturningHeroCatchUpDiagnosticFailure(Exception exception, string message)
    {
        try
        {
            Logger.Error(exception, message);
        }
        catch
        {
            // DEBUG observation must not change the spawn or pending-insertion path.
        }
    }

    private string DescribeDeploymentBlock(
        Mission mission,
        DeploymentMissionController deploymentController,
        bool isOwnAgent,
        bool deploymentBlocked)
    {
        if (deployment.IsCommitted)
            return "deployment-committed";
        if (mission == null)
            return "mission-unavailable";
        if (deploymentController == null)
            return "deployment-controller-unavailable";
        if (!deploymentBlocked)
            return "deployment-does-not-block-record";
        return isOwnAgent
            ? "own-agent-awaiting-deployment-commit"
            : "remote-agent-awaiting-team-setup";
    }
#endif

    public void DrainPendingPuppets()
    {
        if (Mission.Current == null || Mission.Current.DefenderTeam == null) return;
        ApplyPendingPlayerHandoffs();

        BattleAgentSpawnData[] pending;
        lock (pendingPuppetLock)
        {
            if (pendingPuppets.Count == 0) return;
            int count = Math.Min(MaxBufferedSpawnsPerTick, pendingPuppets.Count);
            pending = new BattleAgentSpawnData[count];
            for (int i = 0; i < count; i++)
            {
                int heroIndex = pendingPuppets.FindIndex(IsPlayerHeroRecord);
                int index = heroIndex >= 0 ? heroIndex : 0;
                pending[i] = pendingPuppets[index];
                pendingPuppets.RemoveAt(index);
            }
        }

        // BR-110: count the live remaining capacity ONCE for the whole drain and decrement it as puppets spawn,
        // instead of recounting every mission agent per buffered puppet — a large catch-up backlog at the limit
        // would otherwise do backlog x agentCount checks per frame while nothing can spawn.
        int slotsAvailable = agentBudget.RemainingCapacity(agentBudget.CountLiveAgents(Mission.Current));

        foreach (var data in pending)
        {
            // Per-puppet guard: one bad record must not abort the whole drain (and re-throw every tick). On
            // failure, drop it rather than re-buffering, so it can't spin a per-tick exception loop.
            try
            {
                if (!TrySpawnPuppetNow(data, ref slotsAvailable))
                    lock (pendingPuppetLock) pendingPuppets.Add(data);
            }
            catch (Exception e)
            {
                Logger.Error(e, "[BattleSync] Failed to spawn buffered puppet {AgentId}; dropping it", data.AgentId);
            }
        }
    }

    private IEnumerable<BattleAgentSpawnData> PlayerHeroesFirst(IEnumerable<BattleAgentSpawnData> agents)
    {
        foreach (var data in agents)
            if (IsPlayerHeroRecord(data)) yield return data;
        foreach (var data in agents)
            if (!IsPlayerHeroRecord(data)) yield return data;
    }

    private bool IsPlayerHeroRecord(BattleAgentSpawnData data)
    {
        if (data == null || string.IsNullOrEmpty(data.OwnerControllerId)) return false;
        return playerManager.TryGetPlayer(data.OwnerControllerId, out var player)
            && player.CharacterObjectId == data.CharacterId;
    }

    // The PartyBase for a battle party id (a MapEventParty object-manager id), used for a puppet's origin.
    private PartyBase ResolvePuppetParty(string mapEventPartyId)
    {
        if (mapEventPartyId != null && objectManager.TryGetObject<MapEventParty>(mapEventPartyId, out var mapEventParty))
            return mapEventParty?.Party;
        return null;
    }

    // Any involved party of the given side, for a spawn record whose own party never resolved here.
    private static PartyBase ResolveFallbackParty(BattleSideEnum side)
    {
        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || side == BattleSideEnum.None) return null;

        foreach (var involved in mapEvent.GetMapEventSide(side).Parties)
        {
            if (involved?.Party != null) return involved.Party;
        }

        return null;
    }

    // Another owner's puppet must stay off PlayerTeam because every formation there is locally commandable.
    // Use the side's non-player team; missing main or ally teams are buffered until initialization completes.
    private Team ResolvePuppetTeam(BattleAgentSpawnData data)
    {
        var mainTeam = BattleTeams.Resolve(data.Side);
        if (mainTeam == null) return null;

        // Our OWN troop replicated back to us (e.g. our own-party deployment broadcast echoed over the mesh) belongs
        // on our own team — it is the one puppet we DO control.
        if (session.IsOwn(data.OwnerControllerId))
            return mainTeam;

        var playerTeam = Mission.Current.PlayerTeam;
        if (mainTeam != playerTeam) return mainTeam;          // main team isn't ours (we're an ally) — safe to use

        // The side's main team IS our PlayerTeam, so route to the side's ally team instead so we can't command it.
        var allyTeam = data.Side == BattleSideEnum.Attacker
            ? Mission.Current.AttackerAllyTeam
            : Mission.Current.DefenderAllyTeam;
        if (allyTeam != null && allyTeam != playerTeam) return allyTeam;

        // Never put another player's party on the local command team.
        return null;
    }

    private MissionEquipment ResolveMissionEquipment(MissionEquipmentData data)
    {
        var missionEquipment = new MissionEquipment();
        if (data == null || data.WeaponSlots.Count == 0) return null;

        for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)
        {
            if (!missionWeaponDataMapper.TryResolve(
                    data.WeaponSlots[(int)equipmentIndex],
                    out MissionWeapon weapon))
            {
                return null;
            }

            missionEquipment._weaponSlots[(int)equipmentIndex] = weapon;
        }
        return missionEquipment;
    }
}
