using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
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

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;

    // [Host] Map-event party ids we have already fielded as mid-battle reinforcements, so a repeated involved-
    // parties broadcast for the same party doesn't double-spawn it.
    private readonly HashSet<string> reinforcedParties = new HashSet<string>();

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

        public RecoveryParty(CoopTroopSupplier supplier, string partyId)
        {
            Supplier = supplier;
            PartyId = partyId;
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
        IAgentFormationAssigner formationAssigner)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;

        // [Host] A new AI party joining the live battle is fielded through our own spawn path (reinforcements).
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
        if (!session.IsLocalHost || !deployment.IsCommitted || Mission.Current == null) return;

        try
        {
            TryQueueMigrationReserves();
            FieldMigrationReserves();
        }
        catch (Exception e)
        {
            Logger.Error(e, "[BattleSync] Failed to field migration reserves");
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

        int queuedParties = QueueMissingParties(defenderSupplier, snapshot.KnownPartyIds);
        queuedParties += QueueMissingParties(attackerSupplier, snapshot.KnownPartyIds);

        Logger.Information("[BattleSync] Migration reserve refresh queued {Count} party/parties with no live adopted agents", queuedParties);
    }

    private int QueueMissingParties(CoopTroopSupplier supplier, HashSet<string> knownPartyIds)
    {
        int queued = 0;
        foreach (var (partyId, remaining) in supplier.GetRemainingByParty())
        {
            bool hasLiveAgent = HasLiveOwnedAgent(partyId);
            if (!ShouldRecoverParty(knownPartyIds.Contains(partyId), remaining, hasLiveAgent))
            {
                if (!knownPartyIds.Contains(partyId) && remaining > 0 && hasLiveAgent)
                    Logger.Information("[BattleSync] Migration reserve party {Party} already has adopted agents; leaving its remaining supplier entries untouched", partyId);
                continue;
            }
            if (!recoveryPartyIds.Add(partyId)) continue;

            recoveryParties[(int)supplier.Side].Add(new RecoveryParty(supplier, partyId));
            queued++;
        }
        return queued;
    }

    /// <summary>Whether a refreshed reserve party needs recovery rather than ordinary adoption.</summary>
    public static bool ShouldRecoverParty(bool wasPreviouslyOwned, int remainingTroops, bool hasLiveAgent)
        => !wasPreviouslyOwned && remainingTroops > 0 && !hasLiveAgent;

    private bool HasLiveOwnedAgent(string partyId)
    {
        foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(session.OwnControllerId))
        {
            var agent = info.Agent;
            if (agent == null || agent.IsMount || !agent.IsActive()) continue;
            if (agent.Origin is CoopAgentOrigin origin && origin.MapEventPartyId == partyId)
                return true;
        }
        return false;
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

        var settings = spawnLogic.SpawnSettings;
        var targets = RecoveryTargets.Calculate(
            defenderSupplier.TotalTroops,
            attackerSupplier.TotalTroops,
            spawnLogic.BattleSize,
            settings.MaximumBattleSideRatio,
            settings.DefenderAdvantageFactor);

        CountActiveOwnedHumans(out var activeDefenders, out var activeAttackers);
        var formations = new HashSet<Formation>();
        int spawned = FieldRecoverySide(BattleSideEnum.Defender, targets.Defenders - activeDefenders, formations);
        spawned += FieldRecoverySide(BattleSideEnum.Attacker, targets.Attackers - activeAttackers, formations);

        foreach (var formation in formations)
        {
            formation.SetControlledByAI(true);
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }

        if (spawned > 0)
            Logger.Information("[BattleSync] Fielded {Count} migration reserve troop(s) toward active targets Defender={Def}, Attacker={Atk}",
                spawned, targets.Defenders, targets.Attackers);
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
            var origin = recovery.Supplier.SupplyOneTroopFromParty(recovery.PartyId) as CoopAgentOrigin;
            bool exhausted = recovery.Supplier.GetRemainingForParty(recovery.PartyId) == 0;

            if (origin != null)
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

    // [Host] A party was added to the live battle. If it is a new AI party we field — not a player's own party,
    // and not one of the initial parties the troop supplier already spawns — field it now by spawning its troops
    // at the side's default reinforcement frame. Gated on the battle being activated so the INITIAL involved-
    // parties broadcast (pre-activation) is ignored: those parties spawn through the supplier, not here.
    private void Handle_ReinforcementPartiesAdded(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        if (!session.IsLocalHost) return;
        if (!deployment.IsActivated) return;

        var partyIds = payload.What.MapEventPartyIds;
        if (partyIds == null || partyIds.Length == 0) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            foreach (var partyId in partyIds)
            {
                if (reinforcedParties.Contains(partyId)) continue;      // already fielded
                if (IsSupplierParty(partyId)) continue;                 // an initial party — the supplier spawns it
                if (!objectManager.TryGetObject<MapEventParty>(partyId, out var mapEventParty)) continue;

                var party = mapEventParty?.Party;
                if (party == null) continue;

                // The broadcast is server -> all clients (every battle), so only field this battle's parties.
                var mapEvent = party.MapEventSide?.MapEvent;
                if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId) || mapEventId != session.InstanceId)
                    continue;

                // A player's own party is fielded by that player (Phase E), not us — we only field AI parties.
                if (party.LeaderHero?.IsPlayerHero() == true) continue;

                reinforcedParties.Add(partyId);
                SpawnReinforcementParty(party, partyId);
            }
        });
    }

    // Whether a party is one of the initial reserves the troop supplier already provides, so the native spawn
    // logic spawns it and we must not also spawn it here.
    private bool IsSupplierParty(string mapEventPartyId)
    {
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId))
            foreach (var (partyId, _) in supplier.GetSuppliedByParty())
                if (partyId == mapEventPartyId) return true;
        return false;
    }

    // [Host, game thread] Field a newly-joined AI party: spawn each of its able troops AI-controlled at the
    // side's default reinforcement frame, then put the formations they land in on a charge. Capture is NOT
    // suppressed, so each spawn flows through the owner-side capture pipeline (registered under us, broadcast
    // to peers as puppets, casualty attributed from the origin) — the same pipeline the initial troops use.
    private void SpawnReinforcementParty(PartyBase party, string mapEventPartyId)
    {
        var mission = Mission.Current;
        var team = BattleTeams.Resolve(party.Side);
        if (team == null)
        {
            Logger.Warning("[BattleSync] No team for side {Side}; cannot field reinforcement party {Party}", party.Side, mapEventPartyId);
            return;
        }

        var formations = new HashSet<Formation>();
        int spawned = 0;
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null) continue;

            int able = element.Number - element.WoundedNumber;
            for (int i = 0; i < able; i++)
            {
                var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(MBRandom.RandomInt(int.MaxValue)));
                var agent = SpawnReinforcementTroop(mission, team, origin);
                if (agent?.Formation != null) formations.Add(agent.Formation);
                spawned++;
            }
        }

        // A coop battle has no general commanding formations, so order each formation the reinforcements joined
        // to engage — SetControlledByAI alone leaves them idle without an active behavior.
        foreach (var formation in formations)
        {
            formation.SetControlledByAI(true);
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }

        Logger.Information("[BattleSync] Fielded reinforcement party {Party}: spawned {Count} troop(s)", mapEventPartyId, spawned);

        if (spawned > 0)
        {
            var troopText = spawned > 1 ? $"{spawned} troops" : $"{spawned} troop";
            InformationManager.DisplayMessage(new InformationMessage($"Reinforcements have arrived: {party.Name} ({troopText})"));
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
        buildData.Equipment(equipment);
        buildData.BodyProperties(character.GetBodyPropertiesMax());
        buildData.Controller(AgentControllerType.AI);
        buildData.IsReinforcement(true);
        buildData.ClothingColor1(origin.FactionColor);
        buildData.ClothingColor2(origin.FactionColor2);

        var agent = mission.SpawnAgent(buildData);
        agent.FadeIn();

        formationAssigner.Assign(agent);

        // Wake the AI exactly as the adopt and NPC-release paths do. Without this the reinforcement is
        // AI-controlled but NOT alarmed and holds stale enemy caches, so it ignores its formation's Charge order
        // (set in SpawnReinforcementParty) and stands idle — the "reinforcements spawn but don't move" bug. In a
        // coop battle no general drives the formation, so nothing else alarms them.
        AgentAiWaker.Wake(agent);

        return agent;
    }
}
