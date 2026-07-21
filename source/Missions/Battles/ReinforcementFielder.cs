using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Fields new AI parties that join a live battle and recovers newly-owned reserve parties after host migration
/// when no old-host agents arrived to adopt. Both paths use the local spawn pipeline, so troops are registered,
/// broadcast as puppets, casualty-attributed, assigned to formations, and ordered to charge.
/// </summary>
public interface IReinforcementFielder : IDisposable
{
    /// <summary>[Game thread] Field queued migration reserves as battle capacity becomes available.</summary>
    void Tick();
}

/// <inheritdoc cref="IReinforcementFielder"/>
public class ReinforcementFielder : IReinforcementFielder
{
    private static readonly ILogger Logger = LogManager.GetLogger<ReinforcementFielder>();

    private enum AgentRecoveryResult
    {
        Missing,
        CapturePending,
        Registered,
    }

    private readonly IMessageBroker messageBroker;
    private readonly IBattleNetwork network;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IMissionContext missionContext;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;
    private readonly ICasualtyAttributionMap casualties;
    private readonly Func<DefaultBattleMissionAgentSpawnLogic> spawnLogicProvider;
    private readonly Func<Mission, AgentBuildData, Agent> agentSpawner;

    // Map-event party ids already integrated as mid-battle reinforcements, so a repeated broad broadcast cannot
    // add their reserve depth or immediate entitlement twice.
    private readonly HashSet<string> reinforcedParties = new HashSet<string>();

    private sealed class PendingReinforcementParty
    {
        public readonly string MapEventId;
        public readonly string PartyId;
        public readonly int InitialSpawnCount;
        public bool PhaseIntegrated;
        public int AddedTotal;
        public int InitialTarget;
        public int InitialEntriesConsumed;
        public int InitialTroopsSpawned;
        public bool IsCancelled;
        public CoopAgentOrigin PendingOrigin;
        public Agent PendingSpawnMount;
        public bool PendingSpawnMountCaptured;

        public PendingReinforcementParty(string mapEventId, string partyId, int initialSpawnCount)
        {
            MapEventId = mapEventId;
            PartyId = partyId;
            InitialSpawnCount = initialSpawnCount;
        }
    }

    private readonly object reinforcementGate = new object();
    private readonly Dictionary<string, PendingReinforcementParty> pendingReinforcementParties =
        new Dictionary<string, PendingReinforcementParty>();

    /// <summary>Reserve state captured before the promoted host requests its expanded ownership.</summary>
    private sealed class MigrationReserveSnapshot
    {
        public readonly int DefenderRevision;
        public readonly int AttackerRevision;
        public readonly HashSet<string> KnownPartyIds;

        public MigrationReserveSnapshot(
            int defenderRevision,
            int attackerRevision,
            HashSet<string> knownPartyIds)
        {
            DefenderRevision = defenderRevision;
            AttackerRevision = attackerRevision;
            KnownPartyIds = knownPartyIds;
        }
    }

    /// <summary>A newly-owned party whose authoritative reserve needs local fielding.</summary>
    private sealed class RecoveryParty
    {
        public readonly CoopTroopSupplier Supplier;
        public readonly string PartyId;
        public readonly Queue<CoopAgentOrigin> Origins;
        public CoopAgentOrigin PendingOrigin;
        public Agent PendingSpawnMount;
        public bool PendingSpawnMountCaptured;

        public RecoveryParty(
            CoopTroopSupplier supplier,
            string partyId,
            Queue<CoopAgentOrigin> origins)
        {
            Supplier = supplier;
            PartyId = partyId;
            Origins = origins;
        }
    }

    private readonly object migrationGate = new object();
    private MigrationReserveSnapshot pendingMigration;
    private readonly List<RecoveryParty>[] recoveryParties =
    {
        new List<RecoveryParty>(),
        new List<RecoveryParty>(),
    };
    private readonly int[] recoveryCursors = new int[2];
    private readonly HashSet<string> recoveryPartyIds = new HashSet<string>();
    private readonly List<Agent> pendingAgentSetups = new List<Agent>();

    public ReinforcementFielder(
        IMessageBroker messageBroker,
        IBattleNetwork network,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IMissionContext missionContext,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner,
        ICasualtyAttributionMap casualties,
        Func<DefaultBattleMissionAgentSpawnLogic> spawnLogicProvider = null,
        Func<Mission, AgentBuildData, Agent> agentSpawner = null)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.missionContext = missionContext;
        this.session = session;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;
        this.casualties = casualties;
        this.spawnLogicProvider = spawnLogicProvider
            ?? (() => Mission.Current?.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>());
        this.agentSpawner = agentSpawner ?? ((mission, buildData) => mission.SpawnAgent(buildData));

        // An authority fields a newly-owned AI party through our own spawn path (reinforcements).
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_ReinforcementPartiesAdded);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_ReinforcementPartiesAdded);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public void Tick()
    {
        if (Mission.Current == null) return;

        try
        {
            RetryPendingAgentSetups();

            if (deployment.IsCommitted && deployment.IsActivated)
                FieldPendingReinforcementParties();

            if (!session.IsLocalHost || !deployment.IsCommitted) return;
            TryQueueMigrationReserves();
            FieldMigrationReserves();
        }
        catch (Exception e)
        {
            Logger.Error(e, "[BattleSync] Failed to field reinforcement reserves");
        }
    }

    // The promotion message precedes the request for the successor's expanded reserve. Snapshot the two
    // supplier revisions and known parties so Tick can wait for both replies and identify only newly-owned
    // parties; ordinary reserve resends and initial reserves never enter the recovery path.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (payload.What.MapEventId != session.InstanceId) return;
        if (!TryGetSuppliers(out var defenderSupplier, out var attackerSupplier)) return;

        var knownPartyIds = new HashSet<string>();
        AddPartyIds(defenderSupplier, knownPartyIds);
        AddPartyIds(attackerSupplier, knownPartyIds);

        var snapshot = new MigrationReserveSnapshot(
            defenderSupplier.ReserveRevision,
            attackerSupplier.ReserveRevision,
            knownPartyIds);

        lock (migrationGate)
            pendingMigration = snapshot;
    }

    private static void AddPartyIds(CoopTroopSupplier supplier, HashSet<string> partyIds)
    {
        foreach (var (partyId, _) in supplier.GetRemainingByParty())
            partyIds.Add(partyId);
    }

    // Wait until both reliable-ordered reserve replies have replaced their suppliers. Adoption was queued before
    // the request, so all replayed old-host agents are registered under us before this game-thread scan runs.
    private void TryQueueMigrationReserves()
    {
        MigrationReserveSnapshot snapshot;
        lock (migrationGate)
            snapshot = pendingMigration;

        if (snapshot == null) return;
        if (!TryGetSuppliers(out var defenderSupplier, out var attackerSupplier)) return;
        if (defenderSupplier.ReserveRevision <= snapshot.DefenderRevision) return;
        if (attackerSupplier.ReserveRevision <= snapshot.AttackerRevision) return;

        lock (migrationGate)
        {
            if (!ReferenceEquals(pendingMigration, snapshot)) return;
            pendingMigration = null;
        }

        int queuedParties = QueueMissingParties(defenderSupplier, snapshot);
        queuedParties += QueueMissingParties(attackerSupplier, snapshot);

        Logger.Information("[BattleSync] Migration reserve refresh queued {Count} party/parties with no live adopted agents", queuedParties);
    }

    private int QueueMissingParties(CoopTroopSupplier supplier, MigrationReserveSnapshot snapshot)
    {
        int queued = 0;
        foreach (var (partyId, serverRemaining) in supplier.GetRemainingByParty())
        {
            if (snapshot.KnownPartyIds.Contains(partyId)) continue;
            if (!recoveryPartyIds.Add(partyId)) continue;

            if (!TryBuildRecoveryParty(
                    supplier,
                    partyId,
                    out var recovery,
                    out var activeRoster,
                    out var liveAgents))
            {
                recoveryPartyIds.Remove(partyId);
                continue;
            }

            recoveryParties[(int)supplier.Side].Add(recovery);
            queued++;
            Logger.Information("[BattleSync] Migration reserve party {Party}: roster active={Roster}, locally present={Live}, queued={Queued}, server remaining={Remaining}",
                partyId, activeRoster, liveAgents, recovery.Origins.Count, serverRemaining);
        }
        return queued;
    }

    private bool TryBuildRecoveryParty(
        CoopTroopSupplier supplier,
        string partyId,
        out RecoveryParty recovery,
        out int activeRoster,
        out int liveAgents)
    {
        recovery = null;
        activeRoster = 0;
        liveAgents = 0;
        if (!objectManager.TryGetObjectWithLogging<MapEventParty>(partyId, out var mapEventParty)) return false;
        if (mapEventParty._roster == null)
        {
            Logger.Warning("[BattleSync] Migration reserve party {Party} has no flattened roster; recovery deferred", partyId);
            return false;
        }

        var activeByCharacter = new Dictionary<string, int>();
        var recoverableSeeds = new HashSet<int>();
        var resolvedSeeds = new HashSet<int>();
        foreach (var element in mapEventParty._roster)
        {
            if (element.IsKilled || element.IsWounded || element.IsRouted || element.Troop == null)
            {
                resolvedSeeds.Add(element.Descriptor.UniqueSeed);
                continue;
            }
            if (!objectManager.TryGetId(element.Troop, out var characterId)) continue;

            Increment(activeByCharacter, characterId);
            if (!casualties.WasDeparted(element.Descriptor.UniqueSeed)
                && !supplier.WasDeparted(element.Descriptor.UniqueSeed))
                recoverableSeeds.Add(element.Descriptor.UniqueSeed);
            else
                resolvedSeeds.Add(element.Descriptor.UniqueSeed);
            activeRoster++;
        }

        var liveByCharacter = new Dictionary<string, int>();
        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(controllerId))
            {
                var agent = info.Agent;
                if (agent == null || agent.IsMount || !agent.IsActive()) continue;

                var attribution = casualties.GetOrDefault(info.AgentId);
                if (attribution.MapEventPartyId != partyId) continue;
                if (attribution.TroopCharacterId != null)
                    Increment(liveByCharacter, attribution.TroopCharacterId);
                recoverableSeeds.Remove(attribution.TroopSeed);
                resolvedSeeds.Add(attribution.TroopSeed);
                liveAgents++;
            }
        }

        foreach (var seed in supplier.GetRemainingSeedsForParty(partyId))
            if (casualties.WasDeparted(seed) || supplier.WasDeparted(seed))
                resolvedSeeds.Add(seed);
        supplier.AdvanceResolvedPrefix(partyId, resolvedSeeds);

        var neededByCharacter = CalculateMissingByCharacter(activeByCharacter, liveByCharacter);
        var origins = supplier.ClaimRecoveryTroops(partyId, neededByCharacter, recoverableSeeds);
        if (origins.Count == 0) return false;

        recovery = new RecoveryParty(
            supplier,
            partyId,
            new Queue<CoopAgentOrigin>(origins));
        return true;
    }

    private static void Increment(Dictionary<string, int> counts, string key)
        => counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;

    /// <summary>How many living roster troops are absent locally, grouped by character.</summary>
    public static Dictionary<string, int> CalculateMissingByCharacter(
        IReadOnlyDictionary<string, int> activeRoster,
        IReadOnlyDictionary<string, int> liveAgents)
    {
        var missing = new Dictionary<string, int>();
        foreach (var pair in activeRoster)
        {
            liveAgents.TryGetValue(pair.Key, out var live);
            int count = Math.Max(0, pair.Value - live);
            if (count > 0)
                missing[pair.Key] = count;
        }
        return missing;
    }

    private bool TryGetSuppliers(out CoopTroopSupplier defenderSupplier, out CoopTroopSupplier attackerSupplier)
    {
        defenderSupplier = null;
        attackerSupplier = null;
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId))
        {
            if (supplier.Side == BattleSideEnum.Defender) defenderSupplier = supplier;
            else if (supplier.Side == BattleSideEnum.Attacker) attackerSupplier = supplier;
        }
        return defenderSupplier != null && attackerSupplier != null;
    }

    private void FieldMigrationReserves()
    {
        if (recoveryParties[(int)BattleSideEnum.Defender].Count == 0
            && recoveryParties[(int)BattleSideEnum.Attacker].Count == 0)
            return;

        if (!TryGetSuppliers(out var defenderSupplier, out var attackerSupplier)) return;

        CountActiveOwnedHumans(defenderSupplier, attackerSupplier, out var activeDefenders, out var activeAttackers);
        var slots = RecoverySlots.Calculate(
            defenderSupplier.InitialTroops,
            attackerSupplier.InitialTroops,
            activeDefenders,
            activeAttackers,
            BattleSpawnGate.BattleSize);
        var formations = new HashSet<Formation>();
        int spawned = FieldRecoverySide(BattleSideEnum.Defender, slots.Defenders, formations);
        spawned += FieldRecoverySide(BattleSideEnum.Attacker, slots.Attackers, formations);

        ApplyFormationOrders(formations);

        if (spawned > 0)
            Logger.Information("[BattleSync] Fielded {Count} migration reserve troop(s) within authority slots Defender={Def}, Attacker={Atk}",
                spawned, slots.Defenders, slots.Attackers);
    }

    private void CountActiveOwnedHumans(
        CoopTroopSupplier defenderSupplier,
        CoopTroopSupplier attackerSupplier,
        out int defenders,
        out int attackers)
    {
        defenders = 0;
        attackers = 0;
        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(controllerId))
            {
                var agent = info.Agent;
                if (agent == null || !agent.IsActive() || !agent.IsHuman) continue;

                var attribution = casualties.GetOrDefault(info.AgentId);
                bool isOwned = controllerId == session.OwnControllerId
                    || defenderSupplier.ContainsParty(attribution.MapEventPartyId)
                    || attackerSupplier.ContainsParty(attribution.MapEventPartyId);
                if (!isOwned) continue;

                var side = agent.Team?.Side ?? BattleSideEnum.None;
                if (side == BattleSideEnum.Defender) defenders++;
                else if (side == BattleSideEnum.Attacker) attackers++;
            }
        }

        CountUnregisteredPendingHumans(ref defenders, ref attackers);
    }

    private void CountUnregisteredPendingHumans(ref int defenders, ref int attackers)
    {
        var mission = Mission.Current;
        var countedAgents = new HashSet<Agent>();
        for (int sideIndex = 0; sideIndex < recoveryParties.Length; sideIndex++)
        {
            foreach (var recovery in recoveryParties[sideIndex])
            {
                if (recovery.PendingOrigin == null
                    || !TryFindActiveOriginAgent(mission, recovery.PendingOrigin, out var agent)
                    || !agent.IsHuman
                    || !countedAgents.Add(agent)
                    || coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out _))
                    continue;

                if (sideIndex == (int)BattleSideEnum.Defender) defenders++;
                else attackers++;
            }
        }

        List<PendingReinforcementParty> pending;
        lock (reinforcementGate)
            pending = new List<PendingReinforcementParty>(pendingReinforcementParties.Values);
        foreach (var party in pending)
        {
            if (party.PendingOrigin == null
                || !TryFindActiveOriginAgent(mission, party.PendingOrigin, out var agent)
                || !agent.IsHuman
                || !countedAgents.Add(agent)
                || coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out _))
                continue;

            if (agent.Team?.Side == BattleSideEnum.Defender) defenders++;
            else if (agent.Team?.Side == BattleSideEnum.Attacker) attackers++;
        }
    }

    // Round-robin by party so every missing army party gets represented before one large reserve consumes the
    // whole side's active allocation. Exhausted parties leave the queue; the rest refill future casualty slots.
    private int FieldRecoverySide(BattleSideEnum side, int available, HashSet<Formation> formations)
    {
        var team = BattleTeams.Resolve(side);
        if (team == null) return 0;

        int sideIndex = (int)side;
        var parties = recoveryParties[sideIndex];
        int spawned = 0;
        int attemptsRemaining = 0;
        var deferredPartyIds = new HashSet<string>();
        foreach (var party in parties)
            attemptsRemaining += party.Origins.Count + (party.PendingOrigin == null ? 0 : 1);
        while (parties.Count > 0 && attemptsRemaining-- > 0)
        {
            if (deferredPartyIds.Count == parties.Count) break;

            if (recoveryCursors[sideIndex] >= parties.Count)
                recoveryCursors[sideIndex] = 0;

            int index = recoveryCursors[sideIndex];
            var recovery = parties[index];
            if (deferredPartyIds.Contains(recovery.PartyId))
            {
                recoveryCursors[sideIndex]++;
                continue;
            }

            if (!recovery.Supplier.ContainsParty(recovery.PartyId))
            {
                DespawnPendingLocalAgent(recovery, "reserve scope removed");
                recovery.Origins.Clear();
                ClearPendingRecoveryOrigin(recovery);
                Logger.Information("[BattleSync] Cancelled migration recovery for party {Party}; it left this host's reserve scope", recovery.PartyId);
            }

            var origin = recovery.PendingOrigin;
            if (origin == null && recovery.Origins.Count > 0)
            {
                origin = recovery.Origins.Dequeue();
                recovery.PendingOrigin = origin;
                recovery.PendingSpawnMount = null;
                recovery.PendingSpawnMountCaptured = false;
            }

            if (origin != null)
            {
                Agent agent = null;
                bool lateReplay = false;
                bool existingLocalAgent = false;
                bool spawnAttemptConsumedSlot = false;
                bool unavailableReserveEntry = false;
                var useResult = recovery.Supplier.TryUseClaimedTroop(
                    recovery.PartyId,
                    origin.UniqueSeed,
                    () =>
                    {
                        if (IsUnavailableRecoveryEntry(recovery, origin.UniqueSeed))
                        {
                            unavailableReserveEntry = true;
                            return TryDespawnUnavailableRecoveryAgents(recovery, origin);
                        }

                        if (TryFindActiveOriginAgent(Mission.Current, origin, out var exactAgent))
                        {
                            if (TryGetLivePartyAgent(
                                    recovery.PartyId,
                                    origin.UniqueSeed,
                                    out var replayAgent,
                                    exactAgent))
                            {
                                if (IsStaleMigrationReplay(replayAgent))
                                {
                                    if (!TryTakeOwnershipOfStaleReplay(replayAgent))
                                        return false;

                                    DespawnLocalRecoveryAgent(
                                        replayAgent,
                                        replayAgent.MountAgent,
                                        spawnMountCaptured: true,
                                        recovery.PartyId,
                                        origin.UniqueSeed,
                                        "departed-host late replay lost");
                                }
                                else
                                {
                                    if (DespawnLocalRecoveryAgent(
                                            exactAgent,
                                            recovery.PendingSpawnMount,
                                            recovery.PendingSpawnMountCaptured,
                                            recovery.PartyId,
                                            origin.UniqueSeed,
                                            "registered late replay won"))
                                    {
                                        agent = replayAgent;
                                    }
                                    else
                                    {
                                        DespawnLocalRecoveryAgent(
                                            replayAgent,
                                            replayAgent.MountAgent,
                                            spawnMountCaptured: true,
                                            recovery.PartyId,
                                            origin.UniqueSeed,
                                            "remote exact recovery won");
                                        agent = exactAgent;
                                    }
                                    lateReplay = true;
                                    return true;
                                }
                            }

                            if (TryYieldToRemoteAuthority(origin, exactAgent))
                            {
                                agent = exactAgent;
                                lateReplay = true;
                                return true;
                            }

                            var recoveryResult = RecoverCreatedAgent(Mission.Current, origin, out agent);
                            existingLocalAgent = recoveryResult == AgentRecoveryResult.Registered;
                            if (existingLocalAgent && TryYieldToRemoteAuthority(origin, agent))
                            {
                                existingLocalAgent = false;
                                lateReplay = true;
                            }
                            return existingLocalAgent || lateReplay;
                        }

                        if (TryGetLivePartyAgent(recovery.PartyId, origin.UniqueSeed, out agent))
                        {
                            if (!IsStaleMigrationReplay(agent))
                            {
                                origin.SuppressRemoval();
                                lateReplay = true;
                                return true;
                            }

                            if (!TryTakeOwnershipOfStaleReplay(agent))
                                return false;

                            DespawnLocalRecoveryAgent(
                                agent,
                                agent.MountAgent,
                                spawnMountCaptured: true,
                                recovery.PartyId,
                                origin.UniqueSeed,
                                "departed-host late replay removed before recovery");
                            agent = null;
                        }

                        if (available <= 0) return false;
                        bool created = TryCreateReinforcementAgent(team, origin, recovery.PartyId, out agent);
                        if (created && TryYieldToRemoteAuthority(origin, agent))
                        {
                            lateReplay = true;
                            spawnAttemptConsumedSlot = true;
                        }
                        if (!created && TryFindActiveOriginAgent(Mission.Current, origin, out var pendingAgent))
                        {
                            spawnAttemptConsumedSlot = true;
                            if (!recovery.PendingSpawnMountCaptured)
                            {
                                recovery.PendingSpawnMount = pendingAgent.MountAgent;
                                recovery.PendingSpawnMountCaptured = true;
                            }
                            if (DespawnLocalRecoveryAgent(
                                    pendingAgent,
                                    recovery.PendingSpawnMount,
                                    recovery.PendingSpawnMountCaptured,
                                    recovery.PartyId,
                                    origin.UniqueSeed,
                                    "capture did not register"))
                            {
                                recovery.PendingOrigin = origin.CreateRetryOrigin();
                                recovery.PendingSpawnMount = null;
                                recovery.PendingSpawnMountCaptured = false;
                            }
                        }
                        return created;
                    });

                if (useResult == CoopTroopSupplier.ClaimedTroopUseResult.Committed)
                {
                    ClearPendingRecoveryOrigin(recovery);

                    if (unavailableReserveEntry)
                    {
                        Logger.Information("[BattleSync] Skipped unavailable migration recovery seed {Seed} for party {Party}",
                            origin.UniqueSeed, recovery.PartyId);
                    }
                    else if (lateReplay)
                    {
                        if (spawnAttemptConsumedSlot)
                            available--;
                        Logger.Information("[BattleSync] Skipped migration recovery seed {Seed} for party {Party}; another authority already registered it",
                            origin.UniqueSeed, recovery.PartyId);
                    }
                    else
                    {
                        var context = GetSpawnContext(side);
                        if (context != null)
                        {
                            context._numSpawnedTroops++;
                            var phase = side == BattleSideEnum.Defender
                                ? spawnLogicProvider()?.DefenderActivePhase
                                : spawnLogicProvider()?.AttackerActivePhase;
                            if (phase != null)
                            {
                                phase.InitialSpawnedNumber = Math.Max(
                                    phase.InitialSpawnedNumber,
                                    context.NumberOfActiveTroops);
                                phase.NumberActiveTroops = context.NumberOfActiveTroops;
                            }
                        }
                        TrySetupReinforcementAgent(agent, formations);
                        spawned++;
                        if (!existingLocalAgent)
                            available--;
                    }
                }
                else if (useResult == CoopTroopSupplier.ClaimedTroopUseResult.Deferred)
                {
                    if (spawnAttemptConsumedSlot)
                        available--;
                    deferredPartyIds.Add(recovery.PartyId);
                }
                else
                {
                    DespawnPendingLocalAgent(recovery, "claim removed");
                    ClearPendingRecoveryOrigin(recovery);
                }
            }

            bool exhausted = recovery.PendingOrigin == null && recovery.Origins.Count == 0;
            if (exhausted)
            {
                parties.RemoveAt(index);
                recoveryPartyIds.Remove(recovery.PartyId);
                if (recoveryCursors[sideIndex] >= parties.Count)
                    recoveryCursors[sideIndex] = 0;
            }
            else
            {
                recoveryCursors[sideIndex]++;
            }
        }
        return spawned;
    }

    private bool IsUnavailableRecoveryEntry(RecoveryParty recovery, int troopSeed)
    {
        if (casualties.WasDeparted(troopSeed) || recovery.Supplier.WasDeparted(troopSeed))
            return true;
        if (!objectManager.TryGetObject<MapEventParty>(recovery.PartyId, out var mapEventParty))
            return false;
        return GetRosterEntryState(mapEventParty, troopSeed) == RosterEntryState.Inactive;
    }

    private bool TryDespawnUnavailableRecoveryAgents(RecoveryParty recovery, CoopAgentOrigin origin)
    {
        bool removed = true;
        Agent exactAgent = null;
        if (TryFindActiveOriginAgent(Mission.Current, origin, out exactAgent))
        {
            removed = TryDespawnUnavailableRecoveryAgent(
                exactAgent,
                recovery.PendingSpawnMount,
                recovery.PendingSpawnMountCaptured,
                recovery.PartyId,
                origin.UniqueSeed,
                "migration seed departed before recovery");
        }

        if (TryGetLivePartyAgent(recovery.PartyId, origin.UniqueSeed, out var replayAgent, exactAgent))
        {
            bool replayRemoved = TryDespawnUnavailableRecoveryAgent(
                replayAgent,
                replayAgent.MountAgent,
                spawnMountCaptured: true,
                recovery.PartyId,
                origin.UniqueSeed,
                "departed migration replay removed");
            removed = replayRemoved && removed;
        }

        return removed;
    }

    private bool TryDespawnUnavailableRecoveryAgent(
        Agent agent,
        Agent spawnMount,
        bool spawnMountCaptured,
        string partyId,
        int troopSeed,
        string reason)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (registry.TryGetAgentInfo(agent, out var agentInfo)
            && agentInfo.CurrentAuthority != session.OwnControllerId
            && !TryTakeOwnershipOfStaleReplay(agent))
            return false;

        return DespawnLocalRecoveryAgent(
            agent,
            spawnMount,
            spawnMountCaptured,
            partyId,
            troopSeed,
            reason);
    }

    private static void ClearPendingRecoveryOrigin(RecoveryParty recovery)
    {
        recovery.PendingOrigin = null;
        recovery.PendingSpawnMount = null;
        recovery.PendingSpawnMountCaptured = false;
    }

    private static void ClearPendingReinforcementOrigin(PendingReinforcementParty pending)
    {
        pending.PendingOrigin = null;
        pending.PendingSpawnMount = null;
        pending.PendingSpawnMountCaptured = false;
    }

    private void DespawnPendingLocalAgent(RecoveryParty recovery, string reason)
    {
        var origin = recovery.PendingOrigin;
        if (origin != null && TryFindActiveOriginAgent(Mission.Current, origin, out var agent))
        {
            DespawnLocalRecoveryAgent(
                agent,
                recovery.PendingSpawnMount,
                recovery.PendingSpawnMountCaptured,
                recovery.PartyId,
                origin.UniqueSeed,
                reason);
        }
        else if (recovery.PendingSpawnMountCaptured)
        {
            DespawnLocalRecoveryMount(
                recovery.PendingSpawnMount,
                recovery.PartyId,
                origin?.UniqueSeed ?? 0,
                reason);
        }
    }

    private void DespawnPendingLocalAgent(PendingReinforcementParty pending, string reason)
    {
        var origin = pending.PendingOrigin;
        if (origin != null && TryFindActiveOriginAgent(Mission.Current, origin, out var agent))
        {
            DespawnLocalRecoveryAgent(
                agent,
                pending.PendingSpawnMount,
                pending.PendingSpawnMountCaptured,
                pending.PartyId,
                origin.UniqueSeed,
                reason);
        }
        else if (pending.PendingSpawnMountCaptured)
        {
            DespawnLocalRecoveryMount(
                pending.PendingSpawnMount,
                pending.PartyId,
                origin?.UniqueSeed ?? 0,
                reason);
        }
    }

    private bool DespawnLocalRecoveryAgent(
        Agent agent,
        Agent spawnMount,
        bool spawnMountCaptured,
        string partyId,
        int troopSeed,
        string reason)
    {
        if (agent == null) return false;

        var registry = coopMissionComponent.AgentRegistry;
        if (agent.Origin is CoopAgentOrigin agentOrigin)
            agentOrigin.SuppressRemoval();
        if (registry.TryGetAgentInfo(agent, out var agentInfo)
            && agentInfo.CurrentAuthority != session.OwnControllerId)
        {
            Logger.Information("[BattleSync] Left pending reinforcement seed {Seed} for party {Party} active because authority transferred to {Authority}",
                troopSeed, partyId, agentInfo.CurrentAuthority);
            return false;
        }

        var mount = spawnMountCaptured ? spawnMount : agent.MountAgent;
        bool canRemoveMount = CanRemoveSpawnMount(mount, agent, out var mountInfo);
        bool hideMount = canRemoveMount
            && ReferenceEquals(agent.MountAgent, mount)
            && mount.IsActive();
        Guid? agentId = null;
        Guid? mountId = null;

        if (agentInfo != null)
        {
            agentId = agentInfo.AgentId;
            network.SendAll(new NetworkBattleAgentRouted(
                agentId.Value,
                hideMount,
                isAdministrativeRemoval: true));
        }
        if (canRemoveMount && mountInfo != null)
        {
            mountId = mountInfo.AgentId;
            network.SendAll(new NetworkBattleAgentRouted(
                mountId.Value,
                hideMount: false,
                isAdministrativeRemoval: true));
        }

        if (canRemoveMount && mount?.Origin is CoopAgentOrigin mountOrigin)
            mountOrigin.SuppressRemoval();

        BattleSpawnGate.RunWithAdministrativeRemoval(agent, mount, () =>
        {
            if (agent.IsActive())
                agent.FadeOut(hideInstantly: true, hideMount: hideMount);
            if (canRemoveMount && !hideMount && mount.IsActive())
                mount.FadeOut(hideInstantly: true, hideMount: false);
        });

        if (agentId.HasValue)
        {
            registry.RemoveAgent(agentId.Value);
            casualties.Forget(agentId.Value);
        }
        if (mountId.HasValue)
        {
            registry.RemoveAgent(mountId.Value);
            casualties.Forget(mountId.Value);
        }

        Logger.Information("[BattleSync] Despawned pending local reinforcement seed {Seed} for party {Party}: {Reason}",
            troopSeed, partyId, reason);
        return true;
    }

    private void DespawnLocalRecoveryMount(Agent mount, string partyId, int troopSeed, string reason)
    {
        if (!CanRemoveSpawnMount(mount, spawnRider: null, out var mountInfo)) return;

        var registry = coopMissionComponent.AgentRegistry;
        Guid? mountId = mountInfo?.AgentId;
        if (mountId.HasValue)
            network.SendAll(new NetworkBattleAgentRouted(
                mountId.Value,
                hideMount: false,
                isAdministrativeRemoval: true));

        if (mount.Origin is CoopAgentOrigin mountOrigin)
            mountOrigin.SuppressRemoval();
        BattleSpawnGate.RunWithAdministrativeRemoval(agent: null, spawnMount: mount, remove: () =>
        {
            if (mount.IsActive())
                mount.FadeOut(hideInstantly: true, hideMount: false);
        });

        if (mountId.HasValue)
        {
            registry.RemoveAgent(mountId.Value);
            casualties.Forget(mountId.Value);
        }

        Logger.Information("[BattleSync] Despawned pending local reinforcement mount for seed {Seed} in party {Party}: {Reason}",
            troopSeed, partyId, reason);
    }

    private bool CanRemoveSpawnMount(Agent mount, Agent spawnRider, out CoopAgentInfo mountInfo)
    {
        mountInfo = null;
        if (mount == null) return false;

        var registry = coopMissionComponent.AgentRegistry;
        registry.TryGetAgentInfo(mount, out mountInfo);
        var currentRider = mount.RiderAgent;
        if (currentRider != null && currentRider.IsActive() && !ReferenceEquals(currentRider, spawnRider))
        {
            if (mountInfo != null
                && registry.TryGetAgentInfo(currentRider, out var riderInfo)
                && mountInfo.CurrentAuthority != riderInfo.CurrentAuthority)
                registry.TryTransferAuthority(riderInfo.CurrentAuthority, mountInfo.AgentId);
            return false;
        }

        return mountInfo == null || mountInfo.CurrentAuthority == session.OwnControllerId;
    }

    private bool IsStaleMigrationReplay(Agent agent)
    {
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info)) return false;
        if (session.IsOwn(info.CurrentAuthority)) return false;
        foreach (var controllerId in missionContext.ControllersInMission)
            if (controllerId == info.CurrentAuthority)
                return false;
        return true;
    }

    private bool TryTakeOwnershipOfStaleReplay(Agent agent)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(agent, out var agentInfo)) return false;
        string staleAuthority = agentInfo.CurrentAuthority;
        if (!IsStaleMigrationReplay(agent)) return false;

        CoopAgentInfo mountInfo = null;
        var mount = agent.MountAgent;
        bool mountTransferred = mount != null
            && registry.TryGetAgentInfo(mount, out mountInfo)
            && mountInfo.CurrentAuthority == staleAuthority;
        if (mountTransferred && !registry.TryTransferAuthority(session.OwnControllerId, mountInfo.AgentId))
            return false;

        if (registry.TryTransferAuthority(session.OwnControllerId, agentInfo.AgentId))
            return true;

        if (mountTransferred)
            registry.TryTransferAuthority(staleAuthority, mountInfo.AgentId);
        return false;
    }

    private bool TryYieldToRemoteAuthority(CoopAgentOrigin origin, Agent agent)
    {
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info)
            || info.CurrentAuthority == session.OwnControllerId)
            return false;

        origin.SuppressRemoval();
        return true;
    }

    private bool TryGetLivePartyAgent(
        string partyId,
        int troopSeed,
        out Agent liveAgent,
        Agent excludedAgent = null)
    {
        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(controllerId))
            {
                if (info.Agent == null || !info.Agent.IsActive() || ReferenceEquals(info.Agent, excludedAgent)) continue;
                var attribution = casualties.GetOrDefault(info.AgentId);
                if (attribution.MapEventPartyId == partyId && attribution.TroopSeed == troopSeed)
                {
                    liveAgent = info.Agent;
                    return true;
                }
            }
        }

        liveAgent = null;
        return false;
    }

    /// <summary>Unused active slots within this authority's persistent server allocation.</summary>
    public readonly struct RecoverySlots
    {
        public readonly int Defenders;
        public readonly int Attackers;

        public RecoverySlots(int defenders, int attackers)
        {
            Defenders = defenders;
            Attackers = attackers;
        }

        public static RecoverySlots Calculate(
            int defenderEntitlement,
            int attackerEntitlement,
            int activeDefenders,
            int activeAttackers,
            int battleSize)
        {
            int defenderTarget = Math.Max(0, defenderEntitlement - activeDefenders);
            int attackerTarget = Math.Max(0, attackerEntitlement - activeAttackers);
            int targetTotal = defenderTarget + attackerTarget;
            int available = (int)Math.Max(0L,
                (long)battleSize - Math.Max(0, activeDefenders) - Math.Max(0, activeAttackers));
            if (targetTotal <= available)
                return new RecoverySlots(defenderTarget, attackerTarget);
            if (targetTotal == 0 || available == 0)
                return new RecoverySlots(0, 0);

            int defenders = (int)(((long)available * defenderTarget) / targetTotal);
            return new RecoverySlots(defenders, available - defenders);
        }
    }

    // Queue only the party that this broad snapshot added after the frozen plan. The authoritative reserve follows
    // on the same ordered stream, and Tick integrates it after deployment even when this message arrived early.
    private void Handle_ReinforcementPartiesAdded(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        var message = payload.What;
        if (!string.IsNullOrEmpty(session.InstanceId) && message.MapEventId != session.InstanceId) return;

        var partyIds = message.MapEventPartyIds;
        if (partyIds == null || partyIds.Length == 0) return;
        var initialSpawnCounts = message.InitialSpawnCounts;
        if (initialSpawnCounts == null || initialSpawnCounts.Length != partyIds.Length) return;
        var postPlanAdditions = message.PostPlanAdditions;
        if (postPlanAdditions == null || postPlanAdditions.Length != partyIds.Length) return;

        lock (reinforcementGate)
        {
            for (int index = 0; index < partyIds.Length; index++)
            {
                if (!postPlanAdditions[index]) continue;
                var partyId = partyIds[index];
                if (string.IsNullOrEmpty(partyId) || reinforcedParties.Contains(partyId)) continue;
                if (!pendingReinforcementParties.ContainsKey(partyId))
                    pendingReinforcementParties[partyId] = new PendingReinforcementParty(
                        message.MapEventId, partyId, initialSpawnCounts[index]);
            }
        }
    }

    private void FieldPendingReinforcementParties()
    {
        List<PendingReinforcementParty> pending;
        lock (reinforcementGate)
            pending = new List<PendingReinforcementParty>(pendingReinforcementParties.Values);

        foreach (var party in pending)
        {
            if (!TryIntegrateReinforcementParty(party)) continue;

            lock (reinforcementGate)
            {
                pendingReinforcementParties.Remove(party.PartyId);
                if (!party.IsCancelled)
                    reinforcedParties.Add(party.PartyId);
            }
        }
    }

    // [Game thread] The supplier containing the party proves this client owns it. Extend native reserve depth
    // once, then field only its persistent entitlement through supplier-backed authoritative origins.
    private bool TryIntegrateReinforcementParty(PendingReinforcementParty pending)
    {
        if (string.IsNullOrEmpty(session.InstanceId)) return false;
        if (pending.MapEventId != session.InstanceId) return true;
        if (!TryGetOwningSupplier(pending.PartyId, out var supplier))
        {
            if (!pending.PhaseIntegrated) return false;

            DespawnPendingLocalAgent(pending, "reserve scope removed");
            ClearPendingReinforcementOrigin(pending);
            pending.IsCancelled = true;
            Logger.Information("[BattleSync] Cancelled reinforcement party {Party}; it left this authority's reserve scope",
                pending.PartyId);
            return true;
        }
        if (!objectManager.TryGetObject<MapEventParty>(pending.PartyId, out var mapEventParty)) return false;
        if (mapEventParty._roster == null) return false;

        var party = mapEventParty?.Party;
        if (party == null) return false;
        var mapEvent = party.MapEventSide?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId)) return false;
        if (mapEventId != session.InstanceId) return true;

        // Direct player parties run their own mission entry/deployment path. This path is for AI parties,
        // including AI subordinates owned by a player army leader rather than by the elected host.
        if (party.LeaderHero?.IsPlayerHero() == true) return true;

        var spawnLogic = spawnLogicProvider();
        if (spawnLogic == null || !spawnLogic.IsInitialSpawnOver) return false;
        var phase = party.Side == BattleSideEnum.Defender
            ? spawnLogic.DefenderActivePhase
            : spawnLogic.AttackerActivePhase;
        var context = spawnLogic._battleSideSpawnContexts[(int)party.Side];
        if (phase == null || context == null) return false;
        if (!supplier.TryGetPartyCounts(pending.PartyId, out var partyTotal, out var supplied, out var initial))
            return false;

        if (!pending.PhaseIntegrated)
        {
            int represented = supplier.GetRepresentedPhaseCapacity(pending.PartyId);
            pending.AddedTotal = Math.Max(0, partyTotal - represented);
            if (pending.AddedTotal == 0)
                return true; // the party arrived before initial sizing and Init already included its full reserve
            if (supplied > 0 && IsMigrationRecoveryPending(pending.PartyId))
                return true; // an ownership migration; the migration recovery queue owns this handoff

            pending.InitialTarget = Math.Max(0,
                Math.Min(Math.Min(initial, pending.InitialSpawnCount), pending.AddedTotal));
            phase.TotalSpawnNumber += pending.AddedTotal;
            phase.RemainingSpawnNumber += pending.AddedTotal;
            spawnLogic._numberOfTroopsInTotal[(int)party.Side] += pending.AddedTotal;
            supplier.RecordPhaseCapacity(pending.PartyId, partyTotal);
            pending.PhaseIntegrated = true;
        }

        var team = BattleTeams.Resolve(party.Side);
        if (team == null)
        {
            Logger.Warning("[BattleSync] No team for side {Side}; reinforcement party {Party} remains queued", party.Side, pending.PartyId);
            return false;
        }

        var formations = new HashSet<Formation>();
        bool creationRetryPending = false;
        while (pending.InitialTroopsSpawned < pending.InitialTarget
            && pending.InitialEntriesConsumed < pending.AddedTotal)
        {
            if (pending.PendingOrigin == null)
            {
                if (!supplier.TryClaimOneTroopFromParty(pending.PartyId, out var suppliedOrigin))
                    break;

                if (suppliedOrigin is not CoopAgentOrigin suppliedCoopOrigin)
                {
                    pending.InitialEntriesConsumed++;
                    phase.RemainingSpawnNumber = Math.Max(0, phase.RemainingSpawnNumber - 1);
                    RemoveUnspawnableTroop(spawnLogic, phase, party.Side);
                    continue;
                }

                pending.PendingOrigin = suppliedCoopOrigin;
                pending.PendingSpawnMount = null;
                pending.PendingSpawnMountCaptured = false;
            }

            var origin = pending.PendingOrigin;
            Agent agent = null;
            bool liveAgentWon = false;
            bool unavailableReserveEntry = false;
            var useResult = supplier.TryUseClaimedTroop(
                pending.PartyId,
                origin.UniqueSeed,
                () =>
                {
                    if (casualties.WasDeparted(origin.UniqueSeed)
                        || supplier.WasDeparted(origin.UniqueSeed))
                    {
                        unavailableReserveEntry = true;
                        return true;
                    }

                    if (TryGetLivePartyAgent(pending.PartyId, origin.UniqueSeed, out agent))
                    {
                        if (!IsStaleMigrationReplay(agent))
                        {
                            origin.SuppressRemoval();
                            liveAgentWon = true;
                            return true;
                        }

                        if (!TryTakeOwnershipOfStaleReplay(agent)
                            || !DespawnLocalRecoveryAgent(
                                agent,
                                agent.MountAgent,
                                spawnMountCaptured: true,
                                pending.PartyId,
                                origin.UniqueSeed,
                                "departed-host post-plan replay removed before reinforcement"))
                            return false;
                        agent = null;
                    }

                    if (GetRosterEntryState(mapEventParty, origin.UniqueSeed) == RosterEntryState.Inactive)
                    {
                        unavailableReserveEntry = true;
                        return true;
                    }

                    bool created = TryCreateReinforcementAgent(team, origin, pending.PartyId, out agent);
                    if (created && TryYieldToRemoteAuthority(origin, agent))
                    {
                        liveAgentWon = true;
                    }
                    if (!created
                        && TryFindActiveOriginAgent(Mission.Current, origin, out var pendingAgent))
                    {
                        if (!pending.PendingSpawnMountCaptured)
                        {
                            pending.PendingSpawnMount = pendingAgent.MountAgent;
                            pending.PendingSpawnMountCaptured = true;
                        }
                        if (DespawnLocalRecoveryAgent(
                                pendingAgent,
                                pending.PendingSpawnMount,
                                pending.PendingSpawnMountCaptured,
                                pending.PartyId,
                                origin.UniqueSeed,
                                "capture did not register"))
                        {
                            pending.PendingOrigin = origin.CreateRetryOrigin();
                            pending.PendingSpawnMount = null;
                            pending.PendingSpawnMountCaptured = false;
                        }
                    }
                    return created;
                });
            if (useResult == CoopTroopSupplier.ClaimedTroopUseResult.Deferred)
            {
                creationRetryPending = true;
                break;
            }
            if (useResult == CoopTroopSupplier.ClaimedTroopUseResult.ClaimMissing)
            {
                DespawnPendingLocalAgent(pending, "claim removed");
                ClearPendingReinforcementOrigin(pending);
                creationRetryPending = true;
                break;
            }

            ClearPendingReinforcementOrigin(pending);
            pending.InitialEntriesConsumed++;
            phase.RemainingSpawnNumber = Math.Max(0, phase.RemainingSpawnNumber - 1);
            if (unavailableReserveEntry)
            {
                RemoveUnspawnableTroop(spawnLogic, phase, party.Side);
                Logger.Information("[BattleSync] Skipped unavailable reinforcement seed {Seed} in party {Party}",
                    origin.UniqueSeed, pending.PartyId);
                continue;
            }

            pending.InitialTroopsSpawned++;
            if (liveAgentWon)
            {
                Logger.Information("[BattleSync] Skipped local reinforcement setup for seed {Seed} in party {Party}; a matching live agent already owns it",
                    origin.UniqueSeed, pending.PartyId);
            }
            else
            {
                context._numSpawnedTroops++;
                phase.InitialSpawnedNumber++;
                phase.NumberActiveTroops = context.NumberOfActiveTroops;
                TrySetupReinforcementAgent(agent, formations);
            }
        }

        ApplyFormationOrders(formations);

        Logger.Information("[BattleSync] Integrated reinforcement party {Party}: reserve +{Reserve}, initial {Spawned}/{Target}",
            pending.PartyId, pending.AddedTotal, pending.InitialTroopsSpawned, pending.InitialTarget);

        if (pending.InitialTroopsSpawned > 0)
        {
            var troopText = pending.InitialTroopsSpawned > 1
                ? $"{pending.InitialTroopsSpawned} troops"
                : $"{pending.InitialTroopsSpawned} troop";
            InformationManager.DisplayMessage(new InformationMessage($"Reinforcements have arrived: {party.Name} ({troopText})"));
        }

        return !creationRetryPending;
    }

    private enum RosterEntryState
    {
        Unknown,
        Active,
        Inactive,
    }

    private static RosterEntryState GetRosterEntryState(MapEventParty party, int troopSeed)
    {
        foreach (var element in party._roster)
        {
            if (element.Descriptor.UniqueSeed != troopSeed) continue;
            return element.Troop != null
                && !element.IsKilled
                && !element.IsWounded
                && !element.IsRouted
                ? RosterEntryState.Active
                : RosterEntryState.Inactive;
        }

        return RosterEntryState.Unknown;
    }

    private bool TryGetOwningSupplier(string partyId, out CoopTroopSupplier owningSupplier)
    {
        owningSupplier = null;
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId))
        {
            if (!supplier.ContainsParty(partyId)) continue;
            if (owningSupplier != null)
            {
                Logger.Warning("[BattleSync] Party {Party} appeared in both local side suppliers", partyId);
                return false;
            }
            owningSupplier = supplier;
        }
        return owningSupplier != null;
    }

    private bool IsMigrationRecoveryPending(string partyId)
    {
        lock (migrationGate)
            if (pendingMigration != null)
                return true;

        return recoveryPartyIds.Contains(partyId);
    }

    private static void RemoveUnspawnableTroop(
        DefaultBattleMissionAgentSpawnLogic spawnLogic,
        MissionSpawnPhase phase,
        BattleSideEnum side)
    {
        phase.TotalSpawnNumber = Math.Max(0, phase.TotalSpawnNumber - 1);
        spawnLogic._numberOfTroopsInTotal[(int)side] = Math.Max(
            0, spawnLogic._numberOfTroopsInTotal[(int)side] - 1);
    }

    private MissionBattleSideSpawnContext GetSpawnContext(BattleSideEnum side)
    {
        var spawnLogic = spawnLogicProvider();
        return spawnLogic?._battleSideSpawnContexts[(int)side];
    }

    private bool TryCreateReinforcementAgent(
        Team team,
        CoopAgentOrigin origin,
        string partyId,
        out Agent agent)
    {
        var mission = Mission.Current;
        var recoveryResult = RecoverCreatedAgent(mission, origin, out agent);
        if (recoveryResult != AgentRecoveryResult.Missing)
            return recoveryResult == AgentRecoveryResult.Registered;

        var agentsBeforeAttempt = SnapshotActiveAgents(mission);

        try
        {
            agent = CreateReinforcementAgent(mission, team, origin);
        }
        catch (Exception e)
        {
            recoveryResult = RecoverCreatedAgent(mission, origin, out agent);
            if (recoveryResult == AgentRecoveryResult.Registered)
            {
                Logger.Warning(e, "[BattleSync] Recovered reinforcement seed {Seed} for party {Party} after creation threw",
                    origin.UniqueSeed, partyId);
                return true;
            }

            if (recoveryResult == AgentRecoveryResult.CapturePending)
            {
                Logger.Warning(e, "[BattleSync] Reinforcement seed {Seed} for party {Party} is live after creation threw, but capture is still pending",
                    origin.UniqueSeed, partyId);
                return false;
            }

            int partialMounts = RollbackPartialSpawnMounts(mission, origin, agentsBeforeAttempt);
            Logger.Warning(e, "[BattleSync] Failed to create reinforcement seed {Seed} for party {Party}; faded {PartialMounts} partial mounts and retrying",
                origin.UniqueSeed, partyId, partialMounts);
            return false;
        }

        if (agent != null) return true;

        recoveryResult = RecoverCreatedAgent(mission, origin, out agent);
        if (recoveryResult != AgentRecoveryResult.Missing)
        {
            if (recoveryResult == AgentRecoveryResult.Registered)
                Logger.Warning("[BattleSync] Recovered reinforcement seed {Seed} for party {Party} after creation returned no agent",
                    origin.UniqueSeed, partyId);
            return recoveryResult == AgentRecoveryResult.Registered;
        }

        int nullResultMounts = RollbackPartialSpawnMounts(mission, origin, agentsBeforeAttempt);
        Logger.Warning("[BattleSync] Reinforcement seed {Seed} for party {Party} produced no agent; faded {PartialMounts} partial mounts and retrying",
            origin.UniqueSeed, partyId, nullResultMounts);
        return false;
    }

    private static HashSet<Agent> SnapshotActiveAgents(Mission mission)
    {
        var activeAgents = new HashSet<Agent>();
        if (mission?.Agents == null) return activeAgents;

        foreach (var candidate in mission.Agents)
            if (candidate != null && candidate.IsActive())
                activeAgents.Add(candidate);
        return activeAgents;
    }

    private int RollbackPartialSpawnMounts(
        Mission mission,
        CoopAgentOrigin origin,
        HashSet<Agent> agentsBeforeAttempt)
    {
        var currentAgents = SnapshotActiveAgents(mission);
        var partialMounts = new List<Agent>();
        foreach (var candidate in currentAgents)
        {
            if (agentsBeforeAttempt.Contains(candidate) || !candidate.IsMount) continue;

            var rider = candidate.RiderAgent;
            if (rider == null
                || !ReferenceEquals(rider.Origin, origin)
                || currentAgents.Contains(rider))
                continue;

            if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(candidate, out _))
            {
                Logger.Warning("[BattleSync] Partial mount for reinforcement seed {Seed} was already registered; leaving it active",
                    origin.UniqueSeed);
                continue;
            }

            partialMounts.Add(candidate);
        }

        foreach (var partialMount in partialMounts)
            partialMount.FadeOut(hideInstantly: true, hideMount: false);
        return partialMounts.Count;
    }

    private AgentRecoveryResult RecoverCreatedAgent(Mission mission, CoopAgentOrigin origin, out Agent agent)
    {
        if (!TryFindActiveOriginAgent(mission, origin, out agent))
            return AgentRecoveryResult.Missing;

        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out _))
        {
            messageBroker.Publish(agent, new AgentSpawnedInBattle(agent));
            if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out _))
            {
                Logger.Warning("[BattleSync] Recovered reinforcement seed {Seed}, but capture did not register the live agent; retrying capture",
                    origin.UniqueSeed);
                return AgentRecoveryResult.CapturePending;
            }
        }
        return AgentRecoveryResult.Registered;
    }

    private static bool TryFindActiveOriginAgent(Mission mission, CoopAgentOrigin origin, out Agent agent)
    {
        if (mission?.Agents != null)
            foreach (var candidate in mission.Agents)
                if (candidate != null && candidate.IsActive() && ReferenceEquals(candidate.Origin, origin))
                {
                    agent = candidate;
                    return true;
                }

        agent = null;
        return false;
    }

    // [Authority, game thread] Create one reinforcement troop at the side's reinforcement frame. Setup is kept
    // separate because Mission.SpawnAgent is the transaction's commit point: a later setup failure must retry
    // that live agent rather than spawn the same descriptor again.
    private Agent CreateReinforcementAgent(Mission mission, Team team, CoopAgentOrigin origin)
    {
        var character = (CharacterObject)origin.Troop;
        var equipment = character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment;

        var buildData = new AgentBuildData(character);
        buildData.Team(team);
        buildData.TroopOrigin(origin);
        buildData.Banner(origin.Banner);
        buildData.Equipment(equipment);
        buildData.BodyProperties(character.GetBodyPropertiesMax());
        buildData.Controller(AgentControllerType.AI);
        buildData.IsReinforcement(true);
        buildData.ClothingColor1(origin.FactionColor);
        buildData.ClothingColor2(origin.FactionColor2);

        return agentSpawner(mission, buildData);
    }

    private void TrySetupReinforcementAgent(Agent agent, HashSet<Formation> formations)
    {
        try
        {
            SetupReinforcementAgent(agent);
        }
        catch (Exception e)
        {
            if (!pendingAgentSetups.Contains(agent))
                pendingAgentSetups.Add(agent);
            Logger.Warning(e, "[BattleSync] Reinforcement agent was created but setup failed; retrying the same agent");
        }

        if (agent.Formation != null)
            formations.Add(agent.Formation);
    }

    private void RetryPendingAgentSetups()
    {
        if (pendingAgentSetups.Count == 0) return;

        var formations = new HashSet<Formation>();
        for (int index = pendingAgentSetups.Count - 1; index >= 0; index--)
        {
            var agent = pendingAgentSetups[index];
            if (agent == null || !agent.IsActive())
            {
                pendingAgentSetups.RemoveAt(index);
                continue;
            }
            if (coopMissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info)
                && info.CurrentAuthority != session.OwnControllerId)
            {
                pendingAgentSetups.RemoveAt(index);
                continue;
            }

            try
            {
                SetupReinforcementAgent(agent);
                pendingAgentSetups.RemoveAt(index);
                if (agent.Formation != null)
                    formations.Add(agent.Formation);
            }
            catch (Exception e)
            {
                Logger.Warning(e, "[BattleSync] Reinforcement agent setup retry failed; keeping the same live agent queued");
            }
        }

        ApplyFormationOrders(formations);
    }

    private void SetupReinforcementAgent(Agent agent)
    {
        agent.FadeIn();

        formationAssigner.Assign(agent);

        // Wake the AI exactly as the adopt and NPC-release paths do. Without this the reinforcement is
        // AI-controlled but NOT alarmed and holds stale enemy caches, so it ignores its formation's Charge order
        // (set by the caller) and stands idle — the "reinforcements spawn but don't move" bug. In a
        // coop battle no general drives the formation, so nothing else alarms them.
        AgentAiWaker.Wake(agent);
    }

    private static void ApplyFormationOrders(IEnumerable<Formation> formations)
    {
        foreach (var formation in formations)
        {
            formation.SetControlledByAI(true);
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }
    }
}
