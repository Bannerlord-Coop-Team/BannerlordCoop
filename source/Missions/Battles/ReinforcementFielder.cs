using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Fields newly-owned reserve parties after a server refresh when no agents arrived to adopt. The local spawn
/// pipeline registers and broadcasts them, attributes casualties, assigns formations, and orders them to charge.
/// </summary>
public interface IReinforcementFielder : IDisposable
{
    /// <summary>[Network thread] Snapshot current reserves before this host receives newly-owned parties.</summary>
    void PrepareForReserveOwnershipExpansion();

    /// <summary>[Game thread] Field queued migration reserves as battle capacity becomes available.</summary>
    void Tick();
}

/// <inheritdoc cref="IReinforcementFielder"/>
public class ReinforcementFielder : IReinforcementFielder
{
    private static readonly ILogger Logger = LogManager.GetLogger<ReinforcementFielder>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IBattleAgentBudget agentBudget;

    /// <summary>Reserve state captured before the promoted host requests its expanded ownership.</summary>
    private sealed class MigrationReserveSnapshot
    {
        public readonly int DefenderRevision;
        public readonly int AttackerRevision;
        public readonly HashSet<string> KnownPartyIds;

        public MigrationReserveSnapshot(int defenderRevision, int attackerRevision, HashSet<string> knownPartyIds)
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

        public RecoveryParty(CoopTroopSupplier supplier, string partyId, Queue<CoopAgentOrigin> origins)
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

    public ReinforcementFielder(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner,
        ICasualtyAttributionMap casualties,
        IBattleAgentBudget agentBudget)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;
        this.casualties = casualties;
        this.agentBudget = agentBudget;

        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Subscribe<NetworkBattleReserveOwnershipExpanded>(Handle_ReserveOwnershipExpanded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Unsubscribe<NetworkBattleReserveOwnershipExpanded>(Handle_ReserveOwnershipExpanded);
    }

    public void Tick()
    {
        if (!session.IsLocalHost || Mission.Current == null) return;

        try
        {
            if (!deployment.IsCommitted) return;

            TryQueueMigrationReserves();
            FieldMigrationReserves();
        }
        catch (Exception e)
        {
            Logger.Error(e, "[BattleSync] Failed to field battle reserves");
        }
    }

    // The promotion message precedes the request for the successor's expanded reserve. Snapshot the two
    // supplier revisions and known parties so Tick can wait for both replies and identify only newly-owned
    // parties; ordinary reserve resends and initial reserves never enter the recovery path.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (payload.What.MapEventId != session.InstanceId) return;
        PrepareForReserveOwnershipExpansion();
    }

    private void Handle_ReserveOwnershipExpanded(MessagePayload<NetworkBattleReserveOwnershipExpanded> payload)
    {
        if (payload.What.MapEventId != session.InstanceId) return;
        PrepareForReserveOwnershipExpansion();
    }

    public void PrepareForReserveOwnershipExpansion()
    {
        if (!TryGetSuppliers(out var defenderSupplier, out var attackerSupplier)) return;

        var knownPartyIds = new HashSet<string>();
        AddPartyIds(defenderSupplier, knownPartyIds);
        AddPartyIds(attackerSupplier, knownPartyIds);

        var snapshot = new MigrationReserveSnapshot(
            defenderSupplier.ReserveRevision,
            attackerSupplier.ReserveRevision,
            knownPartyIds);

        lock (migrationGate)
        {
            // A host-migration signal and the server's ownership-expansion signal can describe the same
            // refresh. Keep the earlier snapshot until both side replies have arrived.
            if (pendingMigration == null)
                pendingMigration = snapshot;
        }
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

        int queuedParties = QueueMissingParties(defenderSupplier, snapshot.KnownPartyIds);
        queuedParties += QueueMissingParties(attackerSupplier, snapshot.KnownPartyIds);

        Logger.Information("[BattleSync] Migration reserve refresh queued {Count} party/parties with no live adopted agents", queuedParties);
    }

    private int QueueMissingParties(CoopTroopSupplier supplier, HashSet<string> knownPartyIds)
    {
        int queued = 0;
        foreach (var (partyId, serverRemaining) in supplier.GetRemainingByParty())
        {
            if (knownPartyIds.Contains(partyId)) continue;
            if (!recoveryPartyIds.Add(partyId)) continue;

            if (!TryBuildRecoveryParty(supplier, partyId, out var recovery, out var activeRoster, out var liveAgents))
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
        foreach (var element in mapEventParty._roster)
        {
            if (element.IsKilled || element.IsWounded || element.IsRouted || element.Troop == null) continue;
            if (!objectManager.TryGetId(element.Troop, out var characterId)) continue;

            Increment(activeByCharacter, characterId);
            recoverableSeeds.Add(element.Descriptor.UniqueSeed);
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
                liveAgents++;
            }
        }

        var neededByCharacter = CalculateMissingByCharacter(activeByCharacter, liveByCharacter);
        var origins = supplier.ClaimRecoveryTroops(partyId, neededByCharacter, recoverableSeeds);
        if (origins.Count == 0) return false;

        recovery = new RecoveryParty(supplier, partyId, new Queue<CoopAgentOrigin>(origins));
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

        var mission = Mission.Current;
        var spawnLogic = mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
        if (spawnLogic == null || !TryGetSuppliers(out var defenderSupplier, out var attackerSupplier)) return;
        if (defenderSupplier.BattleSize <= 0 || defenderSupplier.BattleSize != attackerSupplier.BattleSize) return;

        var settings = spawnLogic.SpawnSettings;
        var targets = RecoveryTargets.Calculate(
            defenderSupplier.SideTotalTroops,
            attackerSupplier.SideTotalTroops,
            defenderSupplier.BattleSize,
            settings.MaximumBattleSideRatio,
            settings.DefenderAdvantageFactor);
        int defenderOwnedTarget = defenderSupplier.OwnedShareOf(targets.Defenders);
        int attackerOwnedTarget = attackerSupplier.OwnedShareOf(targets.Attackers);

        CountActiveOwnedHumans(out var activeOwnedDefenders, out var activeOwnedAttackers);
        CountActiveHumans(out var activeDefenders, out var activeAttackers);
        var formations = new HashSet<Formation>();
        int defenderAvailable = AvailableRecoverySlots(
            defenderOwnedTarget, activeOwnedDefenders, targets.Defenders, activeDefenders);
        int attackerAvailable = AvailableRecoverySlots(
            attackerOwnedTarget, activeOwnedAttackers, targets.Attackers, activeAttackers);
        int spawned = FieldRecoverySide(BattleSideEnum.Defender, defenderAvailable, formations);
        spawned += FieldRecoverySide(BattleSideEnum.Attacker, attackerAvailable, formations);

        ChargeFormations(formations);

        if (spawned > 0)
            Logger.Information("[BattleSync] Fielded {Count} reserve troop(s) toward owned targets Defender={Def}, Attacker={Atk}",
                spawned, defenderOwnedTarget, attackerOwnedTarget);
    }

    private void CountActiveOwnedHumans(out int defenders, out int attackers)
    {
        defenders = 0;
        attackers = 0;
        foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(session.OwnControllerId))
        {
            var agent = info.Agent;
            if (agent == null || !agent.IsActive() || !agent.IsHuman) continue;

            var side = agent.Team?.Side ?? BattleSideEnum.None;
            if (side == BattleSideEnum.Defender) defenders++;
            else if (side == BattleSideEnum.Attacker) attackers++;
        }
    }

    private static void CountActiveHumans(out int defenders, out int attackers)
    {
        defenders = 0;
        attackers = 0;
        foreach (var agent in Mission.Current.Agents)
        {
            if (agent == null || !agent.IsActive() || !agent.IsHuman) continue;
            var side = agent.Team?.Side ?? BattleSideEnum.None;
            if (side == BattleSideEnum.Defender) defenders++;
            else if (side == BattleSideEnum.Attacker) attackers++;
        }
    }

    internal static int AvailableRecoverySlots(int ownedTarget, int activeOwned, int sideTarget, int activeSide)
        => Math.Max(0, Math.Min(ownedTarget - activeOwned, sideTarget - activeSide));

    // Round-robin by party so every missing army party gets represented before one large reserve consumes the
    // whole side's active allocation. Exhausted parties leave the queue; the rest refill future casualty slots.
    private int FieldRecoverySide(BattleSideEnum side, int available, HashSet<Formation> formations)
    {
        if (available <= 0) return 0;

        var team = BattleTeams.Resolve(side);
        if (team == null) return 0;

        int sideIndex = (int)side;
        var parties = recoveryParties[sideIndex];
        int spawned = 0;
        while (available > 0 && parties.Count > 0)
        {
            if (recoveryCursors[sideIndex] >= parties.Count)
                recoveryCursors[sideIndex] = 0;

            int index = recoveryCursors[sideIndex];
            var recovery = parties[index];
            if (!recovery.Supplier.ContainsParty(recovery.PartyId))
            {
                recovery.Origins.Clear();
                Logger.Information("[BattleSync] Cancelled migration recovery for party {Party}; it left this host's reserve scope", recovery.PartyId);
            }

            // BR-110: stop at the engine agent limit, sized to the next origin's slots (mounted = 2); the
            // recovery queues persist, so the next Tick resumes fielding as removals free capacity. An empty
            // queue (null next) skips the check so the exhausted party is still cleaned up below.
            var nextOrigin = recovery.Origins.Count > 0 ? recovery.Origins.Peek() : null;
            if (nextOrigin != null && !agentBudget.HasCapacityFor(Mission.Current, SlotsForOrigin(nextOrigin))) break;

            var origin = recovery.Origins.Count > 0 ? recovery.Origins.Dequeue() : null;
            bool exhausted = recovery.Origins.Count == 0;

            if (origin != null && HasLivePartySeed(recovery.PartyId, origin.UniqueSeed))
            {
                Logger.Information("[BattleSync] Skipped migration recovery seed {Seed} for party {Party}; a late replay already registered it",
                    origin.UniqueSeed, recovery.PartyId);
            }
            else if (origin != null)
            {
                var agent = SpawnReinforcementTroop(Mission.Current, team, origin);
                if (agent?.Formation != null) formations.Add(agent.Formation);
                spawned++;
                available--;
            }

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

    private bool HasLivePartySeed(string partyId, int troopSeed)
    {
        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(controllerId))
            {
                if (info.Agent == null || !info.Agent.IsActive()) continue;
                var attribution = casualties.GetOrDefault(info.AgentId);
                if (attribution.MapEventPartyId == partyId && attribution.TroopSeed == troopSeed)
                    return true;
            }
        }
        return false;
    }

    /// <summary>Joint active troop targets using the same battle-size allocation as native Init.</summary>
    public readonly struct RecoveryTargets
    {
        public readonly int Defenders;
        public readonly int Attackers;

        public RecoveryTargets(int defenders, int attackers)
        {
            Defenders = defenders;
            Attackers = attackers;
        }

        public static RecoveryTargets Calculate(int defenderTotal, int attackerTotal, int battleSize,
            float maximumSideRatio, float defenderAdvantageFactor)
        {
            int combined = defenderTotal + attackerTotal;
            if (combined <= 0 || battleSize <= 0)
                return new RecoveryTargets(0, 0);

            float defenderRatio = (float)defenderTotal / combined;
            float attackerRatio = (float)attackerTotal / combined;
            defenderRatio = Math.Min(maximumSideRatio, defenderRatio * defenderAdvantageFactor);
            attackerRatio = 1f - defenderRatio;

            bool defenderIsLarger = defenderRatio >= attackerRatio;
            if (defenderIsLarger && defenderRatio > maximumSideRatio)
            {
                defenderRatio = maximumSideRatio;
                attackerRatio = 1f - maximumSideRatio;
            }
            else if (!defenderIsLarger && attackerRatio > maximumSideRatio)
            {
                attackerRatio = maximumSideRatio;
                defenderRatio = 1f - maximumSideRatio;
            }

            int defenderTarget;
            int attackerTarget;
            if (defenderRatio < attackerRatio)
            {
                defenderTarget = Math.Min((int)Math.Ceiling(defenderRatio * battleSize), defenderTotal);
                attackerTarget = Math.Min(battleSize - defenderTarget, attackerTotal);
            }
            else
            {
                attackerTarget = Math.Min((int)Math.Ceiling(attackerRatio * battleSize), attackerTotal);
                defenderTarget = Math.Min(battleSize - attackerTarget, defenderTotal);
            }

            return new RecoveryTargets(defenderTarget, attackerTarget);
        }
    }

    // BR-110: render slots a reinforcement origin consumes when spawned — a mounted troop spawns a rider and a
    // horse (2), everyone else 1. The shared budget reads the same equipment SpawnReinforcementTroop spawns
    // from; a null origin keeps its historical rider-only cost (call sites never pass one).
    private int SlotsForOrigin(CoopAgentOrigin origin)
        => origin == null ? 1 : agentBudget.SlotsForOrigin(origin);

    // A coop battle has no general commanding formations, so order each formation the reinforcements joined
    // to engage — SetControlledByAI alone leaves them idle without an active behavior.
    private static void ChargeFormations(HashSet<Formation> formations)
    {
        foreach (var formation in formations)
        {
            formation.SetControlledByAI(true);
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }
    }

    // [Host, game thread] Spawn one reinforcement troop AI-controlled. With no InitialPosition set, the engine
    // positions it at the side's reinforcement spawn frame; we then drop it into its troop-class formation.
    private Agent SpawnReinforcementTroop(Mission mission, Team team, CoopAgentOrigin origin)
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

        var agent = mission.SpawnAgent(buildData);
        agent.FadeIn();

        formationAssigner.Assign(agent);

        // Wake the AI after assigning it to the charged recovery formation, otherwise it can retain stale
        // enemy caches and stand idle.
        AgentAiWaker.Wake(agent);

        return agent;
    }
}
