using Common.Logging;
using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>The reserves a controller owns on one battle side (one entry per owned party; empty if none).</summary>
public readonly struct SideReserve
{
    public readonly BattleSideEnum Side;
    public readonly PartyReserve[] Parties;

    public SideReserve(BattleSideEnum side, PartyReserve[] parties)
    {
        Side = side;
        Parties = parties;
    }
}

/// <summary>One frozen initial-spawn slot reassigned from a departed troop to a waiting player party.</summary>
public readonly struct BattleInitialSpawnTransfer
{
    public readonly long TransferId;
    public readonly string WaitingPartyId;
    public readonly string DonorPartyId;

    public BattleInitialSpawnTransfer(long transferId, string waitingPartyId, string donorPartyId)
    {
        TransferId = transferId;
        WaitingPartyId = waitingPartyId;
        DonorPartyId = donorPartyId;
    }
}

/// <summary>One authoritative priority-wait state replayed to a client entering an existing battle.</summary>
public readonly struct BattlePrioritySlotState
{
    public readonly string WaitingPartyId;
    public readonly long TransferId;
    public readonly string DonorPartyId;
    public readonly bool IsConsumed;

    public BattlePrioritySlotState(
        string waitingPartyId,
        long transferId,
        string donorPartyId,
        bool isConsumed)
    {
        WaitingPartyId = waitingPartyId;
        TransferId = transferId;
        DonorPartyId = donorPartyId;
        IsConsumed = isConsumed;
    }
}

/// <summary>
/// [Server] Builds the authoritative <see cref="IBattleTroopLedger"/> for a battle by flattening every
/// party's roster once (the server owns the resulting descriptor seeds), and resolves which reserves a given
/// controller owns: its own party always, plus — when it is the host — every AI/enemy party that no connected
/// player owns. The host handler sends those reserves to the entering client to feed its troop supplier.
/// </summary>
public interface IBattleTroopReserveBuilder : IGameAbstraction
{
    /// <summary>Freeze this battle's persistent initial-spawn entitlements from the current full party reserves.</summary>
    void PreparePlan(MapEvent mapEvent, int battleSize);

    /// <summary>
    /// The reserves <paramref name="controllerId"/> currently owns. A party whose resolved owning controller
    /// is in <paramref name="absentControllers"/> (a member explicitly marked absent without withdrawing) is
    /// treated as unowned, so it falls to the host. Player registrations survive that absence, so ownership
    /// alone cannot see it — the absent set is what re-scopes the party.
    /// </summary>
    IReadOnlyList<SideReserve> GetOwnedReserves(MapEvent mapEvent, string controllerId, bool isHost,
        IReadOnlyCollection<string> absentControllers = null);

    /// <summary>Allocate a party added after plan preparation from unassigned slots. Returns the party's
    /// persistent initial-spawn entitlement and reports whether this call added it to the frozen plan. A direct
    /// player party that finds the plan full waits for the next transferred slot instead of remaining stranded.</summary>
    int GrantUnassignedInitialSpawns(MapEvent mapEvent, MapEventParty party, out bool isPostPlanAddition,
        out bool waitsForPrioritySlot);

    /// <summary>Atomically move one frozen initial-spawn slot from the departed troop's party to the first live
    /// player party waiting for capacity. The total assigned battle size remains unchanged.</summary>
    bool TryTransferInitialSpawnOnDeparture(MapEvent mapEvent, string donorPartyId,
        out BattleInitialSpawnTransfer transfer);

    /// <summary>Transfer a departed slot and report any consumed ownership chain that stopped replaying.</summary>
    bool TryTransferInitialSpawnOnDeparture(MapEvent mapEvent, string donorPartyId,
        out BattleInitialSpawnTransfer transfer, out BattleInitialSpawnTransfer settledTransfer);

    /// <summary>Retain every remaining initial entitlement of a party unavailable for initial spawning so future
    /// player joiners can claim those already-vacant slots even when no waiter exists at departure time.</summary>
    int RetainInitialSpawnVacancies(MapEvent mapEvent, string donorPartyId);

    /// <summary>Move one retained vacancy to the first live player party waiting for capacity.</summary>
    bool TryTransferRetainedInitialSpawn(MapEvent mapEvent,
        out BattleInitialSpawnTransfer transfer, out BattleInitialSpawnTransfer settledTransfer);

    /// <summary>Release an assignment for a departed player. The same transfer is reassigned
    /// to the next live waiter when possible; otherwise its entitlement is restored to the original donor.</summary>
    bool TryReassignOrReleasePrioritySlot(MapEvent mapEvent, string departedWaitingPartyId,
        out BattleInitialSpawnTransfer transfer, out bool released);

    /// <summary>Controller-based form used when a disconnect has already removed the party from the map event.</summary>
    bool TryReassignOrReleasePrioritySlotForController(MapEvent mapEvent, string controllerId,
        out BattleInitialSpawnTransfer transfer, out bool released);

    /// <summary>Decline an exact, unconsumed assignment when its priority troop cannot spawn.</summary>
    bool TryDeclinePrioritySlot(MapEvent mapEvent, long transferId, string waitingPartyId,
        out BattleInitialSpawnTransfer transfer, out bool released);

    /// <summary>Mark the matching assignment consumed after the waiting player's priority troop spawns while
    /// retaining its ownership chain for a later departure.</summary>
    bool CompletePrioritySpawn(MapEvent mapEvent, long transferId, string waitingPartyId);

    /// <summary>Snapshot all queued, assigned, and consumed priority states for a battle.</summary>
    IReadOnlyList<BattlePrioritySlotState> GetPrioritySlotSnapshot(MapEvent mapEvent);

    /// <summary>Forget a controller's withdrawn parties: drop them from the ledger and the built-set so that,
    /// if it rejoins, its party is re-flattened fresh (supplied pointer reset) and re-spawns.</summary>
    void ForgetController(MapEvent mapEvent, string controllerId);

    /// <summary>Re-flatten every live reserve with reset supplied pointers while preserving the battle's
    /// frozen initial-spawn plan.</summary>
    void RebuildMapEventReserves(MapEvent mapEvent);

    /// <summary>Forget EVERY reserve of a battle (its whole ledger entry + flatten cache). Called when a battle
    /// ENDS — concluded (victory) or fully ABANDONED (the mission instance is empty) — so the server stops
    /// holding the battle's reserves and a later battle on the SAME map event re-flattens all parties fresh
    /// (otherwise the AI/enemy parties the host had been fielding keep their advanced supplied pointers and
    /// never re-spawn on a restart).</summary>
    void ForgetMapEvent(MapEvent mapEvent);
}

/// <inheritdoc cref="IBattleTroopReserveBuilder"/>
public class BattleTroopReserveBuilder : IBattleTroopReserveBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleTroopReserveBuilder>();
    private const string UnownedAiAuthorityId = "\0unowned-ai";

    private sealed class BattleSpawnPlan
    {
        public readonly int BattleSize;
        public readonly Dictionary<string, int> InitialSpawns;
        public readonly Dictionary<string, BattleInitialSpawnParty> Parties;
        public readonly Queue<string> WaitingPlayerParties = new Queue<string>();
        public readonly HashSet<string> WaitingPlayerPartyIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> ForgottenPlayerPartyIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, BattleInitialSpawnTransfer> PriorityTransfers =
            new Dictionary<string, BattleInitialSpawnTransfer>(StringComparer.Ordinal);
        public readonly HashSet<string> ConsumedPriorityTransferPartyIds =
            new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> SettledPriorityTransferPartyIds =
            new HashSet<string>(StringComparer.Ordinal);
        public readonly Queue<string> VacantDonorParties = new Queue<string>();
        public readonly Dictionary<string, int> VacantInitialSpawns =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public int AssignedCount;
        public long LastTransferId;

        public BattleSpawnPlan(
            int battleSize,
            IReadOnlyList<BattleInitialSpawnParty> parties,
            IReadOnlyDictionary<string, int> initialSpawns)
        {
            BattleSize = battleSize;
            Parties = parties.ToDictionary(party => party.PartyId, StringComparer.Ordinal);
            InitialSpawns = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var allocation in initialSpawns)
            {
                InitialSpawns[allocation.Key] = allocation.Value;
                AssignedCount += allocation.Value;
            }
        }
    }

    private readonly IBattleTroopLedger ledger;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleInitialSpawnAllocator initialSpawnAllocator;

    // Parties already flattened into the ledger (by object-manager id). Per-PARTY, not per-map-event, so a
    // party that joins AFTER the battle started (a mid-battle joiner) gets flattened on demand the next time
    // reserves are built — otherwise it would never be in the ledger and that player would own nothing.
    private readonly HashSet<string> builtParties = new HashSet<string>();
    private readonly Dictionary<string, BattleSpawnPlan> spawnPlans = new Dictionary<string, BattleSpawnPlan>();
    private readonly object gate = new object();

    public BattleTroopReserveBuilder(
        IBattleTroopLedger ledger,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleInitialSpawnAllocator initialSpawnAllocator)
    {
        this.ledger = ledger;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.initialSpawnAllocator = initialSpawnAllocator;
    }

    public void PreparePlan(MapEvent mapEvent, int battleSize)
    {
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        lock (gate)
        {
            if (spawnPlans.ContainsKey(mapEventId))
                return;

            EnsureBuilt(mapEvent, mapEventId);

            var parties = new List<BattleInitialSpawnParty>();
            foreach (var party in EnumerateParties(mapEvent))
            {
                if (!objectManager.TryGetId(party, out var partyId)) continue;
                if (!ledger.TryGetReserve(mapEventId, partyId, out var entries, out _)) continue;

                bool isDirectPlayerParty = TryGetOwningPlayer(party, out var partyOwnerController);
                TryGetArmyLeaderPlayer(party, out var armyLeaderController);
                var owningController = ResolveOwningController(partyOwnerController, armyLeaderController);
                // AI parties without a player owner share one allocation group. The elected battle host later
                // fields that group, but the dedicated server itself never owns a party.
                var authorityId = owningController ?? UnownedAiAuthorityId;
                var side = party.Party?.Side ?? BattleSideEnum.None;
                parties.Add(new BattleInitialSpawnParty(
                    partyId, authorityId, side, entries.Count, isDirectPlayerParty));
            }

            int frozenBattleSize = Math.Max(0, battleSize);
            var allocations = initialSpawnAllocator.Allocate(frozenBattleSize, parties);
            var plan = new BattleSpawnPlan(frozenBattleSize, parties, allocations);
            spawnPlans[mapEventId] = plan;

            Logger.Information("[TroopSupply] Prepared battle plan {MapEventId}: {Assigned}/{BattleSize} initial slots across {Parties} parties",
                mapEventId, plan.AssignedCount, frozenBattleSize, parties.Count);
        }
    }

    public int GrantUnassignedInitialSpawns(
        MapEvent mapEvent,
        MapEventParty party,
        out bool isPostPlanAddition,
        out bool waitsForPrioritySlot)
    {
        isPostPlanAddition = false;
        waitsForPrioritySlot = false;
        if (mapEvent == null || party == null) return 0;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return 0;
        if (!objectManager.TryGetId(party, out var partyId)) return 0;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan))
                return 0;

            EnsureBuilt(mapEvent, mapEventId);
            if (!ledger.TryGetReserve(mapEventId, partyId, out var entries, out _))
                return 0;

            isPostPlanAddition = !plan.InitialSpawns.ContainsKey(partyId);
            bool isDirectPlayerParty = TryGetOwningPlayer(party, out var partyOwnerController);
            TryGetArmyLeaderPlayer(party, out var armyLeaderController);
            var authorityId = ResolveOwningController(partyOwnerController, armyLeaderController)
                ?? UnownedAiAuthorityId;
            var partyPlan = new BattleInitialSpawnParty(
                partyId,
                authorityId,
                party.Party?.Side ?? BattleSideEnum.None,
                entries.Count,
                isDirectPlayerParty);

            bool wasForgotten = plan.ForgottenPlayerPartyIds.Remove(partyId);
            if (wasForgotten)
                ClearRetainedInitialSpawns(plan, partyId);

            int granted = GetOrGrantInitialSpawns(plan, partyPlan);
            if (granted == 0 && partyPlan.Capacity > 0 && partyPlan.IsDirectPlayerParty
                && plan.AssignedCount >= plan.BattleSize
                && !plan.PriorityTransfers.Values.Any(transfer => transfer.DonorPartyId == partyId))
            {
                QueueWaitingPlayerParty(plan, partyId);
            }

            waitsForPrioritySlot = plan.WaitingPlayerPartyIds.Contains(partyId);
            Logger.Information("[TroopSupply] Party {PartyId} has {Entitlement} initial slots in battle {MapEventId}; post-plan addition={PostPlan}, waiting={Waiting}",
                partyId, granted, mapEventId, isPostPlanAddition, waitsForPrioritySlot);
            return granted;
        }
    }

    public bool TryTransferInitialSpawnOnDeparture(
        MapEvent mapEvent,
        string donorPartyId,
        out BattleInitialSpawnTransfer transfer)
    {
        return TryTransferInitialSpawnOnDeparture(
            mapEvent,
            donorPartyId,
            out transfer,
            out _);
    }

    public bool TryTransferInitialSpawnOnDeparture(
        MapEvent mapEvent,
        string donorPartyId,
        out BattleInitialSpawnTransfer transfer,
        out BattleInitialSpawnTransfer settledTransfer)
    {
        transfer = default;
        settledTransfer = default;
        if (mapEvent == null || string.IsNullOrEmpty(donorPartyId)) return false;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return false;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan))
                return false;
            if (!plan.Parties.ContainsKey(donorPartyId))
                return false;
            if (!plan.InitialSpawns.TryGetValue(donorPartyId, out var donorInitialSpawns)
                || donorInitialSpawns <= 0)
                return false;

            if (TryTakeNextWaitingPlayerParty(
                    mapEvent,
                    mapEventId,
                    plan,
                    out var waitingPartyId))
            {
                transfer = AssignPrioritySlot(
                    plan,
                    waitingPartyId,
                    donorPartyId,
                    out settledTransfer);

                Logger.Information("[TroopSupply] Transferred initial slot {TransferId} in battle {MapEventId}: {DonorPartyId} -> {WaitingPartyId}; assigned remains {Assigned}/{BattleSize}",
                    transfer.TransferId, mapEventId, donorPartyId, waitingPartyId,
                    plan.AssignedCount, plan.BattleSize);
                return true;
            }

            if (plan.ConsumedPriorityTransferPartyIds.Contains(donorPartyId)
                && !plan.SettledPriorityTransferPartyIds.Contains(donorPartyId)
                && plan.PriorityTransfers.TryGetValue(donorPartyId, out settledTransfer))
            {
                plan.SettledPriorityTransferPartyIds.Add(donorPartyId);
                Logger.Information("[TroopSupply] Settled consumed priority slot {TransferId} in battle {MapEventId} for party {PartyId}; its native replacement retains the slot",
                    settledTransfer.TransferId, mapEventId, donorPartyId);
            }

            RetainVacantInitialSpawns(plan, donorPartyId, 1, replace: false);
            Logger.Information("[TroopSupply] Retained vacant initial slot in battle {MapEventId} for donor party {DonorPartyId}",
                mapEventId, donorPartyId);
            return settledTransfer.TransferId > 0;
        }
    }

    public bool TryReassignOrReleasePrioritySlot(
        MapEvent mapEvent,
        string departedWaitingPartyId,
        out BattleInitialSpawnTransfer transfer,
        out bool released)
    {
        transfer = default;
        released = false;
        if (mapEvent == null || string.IsNullOrEmpty(departedWaitingPartyId)) return false;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return false;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan))
                return false;

            return TryReassignOrReleasePrioritySlot(
                mapEvent,
                mapEventId,
                plan,
                departedWaitingPartyId,
                out transfer,
                out released);
        }
    }

    public bool TryReassignOrReleasePrioritySlotForController(
        MapEvent mapEvent,
        string controllerId,
        out BattleInitialSpawnTransfer transfer,
        out bool released)
    {
        transfer = default;
        released = false;
        if (mapEvent == null || string.IsNullOrEmpty(controllerId)) return false;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return false;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan))
                return false;

            string waitingPartyId = plan.Parties.Values
                .Where(party => party.IsDirectPlayerParty
                    && string.Equals(party.AuthorityId, controllerId, StringComparison.Ordinal)
                    && plan.PriorityTransfers.ContainsKey(party.PartyId))
                .Select(party => party.PartyId)
                .OrderBy(partyId => partyId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (waitingPartyId == null)
            {
                waitingPartyId = plan.Parties.Values
                    .Where(party => party.IsDirectPlayerParty
                        && string.Equals(party.AuthorityId, controllerId, StringComparison.Ordinal)
                        && plan.WaitingPlayerPartyIds.Contains(party.PartyId))
                    .Select(party => party.PartyId)
                    .OrderBy(partyId => partyId, StringComparer.Ordinal)
                    .FirstOrDefault();
            }
            if (waitingPartyId == null)
                return false;

            return TryReassignOrReleasePrioritySlot(
                mapEvent,
                mapEventId,
                plan,
                waitingPartyId,
                out transfer,
                out released);
        }
    }

    private bool TryReassignOrReleasePrioritySlot(
        MapEvent mapEvent,
        string mapEventId,
        BattleSpawnPlan plan,
        string departedWaitingPartyId,
        out BattleInitialSpawnTransfer transfer,
        out bool released)
    {
        transfer = default;
        released = false;

        bool wasQueued = plan.WaitingPlayerPartyIds.Remove(departedWaitingPartyId);
        if (wasQueued)
            RemoveWaitingPlayerParty(plan, departedWaitingPartyId);
        if (!plan.PriorityTransfers.TryGetValue(departedWaitingPartyId, out var activeTransfer))
        {
            if (!wasQueued)
                return false;

            transfer = new BattleInitialSpawnTransfer(0, departedWaitingPartyId, null);
            released = true;
            Logger.Information("[TroopSupply] Cancelled queued priority spawn in battle {MapEventId} for departed party {DepartedPartyId}",
                mapEventId, departedWaitingPartyId);
            return true;
        }
        if (!plan.InitialSpawns.ContainsKey(departedWaitingPartyId)
            || !plan.InitialSpawns.TryGetValue(activeTransfer.DonorPartyId, out var donorInitialSpawns))
            return false;

        plan.PriorityTransfers.Remove(departedWaitingPartyId);
        plan.ConsumedPriorityTransferPartyIds.Remove(departedWaitingPartyId);
        plan.SettledPriorityTransferPartyIds.Remove(departedWaitingPartyId);
        plan.InitialSpawns[departedWaitingPartyId] = 0;

        while (plan.WaitingPlayerParties.Count > 0)
        {
            var nextWaitingPartyId = plan.WaitingPlayerParties.Dequeue();
            if (!plan.WaitingPlayerPartyIds.Remove(nextWaitingPartyId))
                continue;
            if (!IsLiveWaitingPlayerParty(mapEvent, mapEventId, plan, nextWaitingPartyId))
                continue;

            transfer = new BattleInitialSpawnTransfer(
                activeTransfer.TransferId,
                nextWaitingPartyId,
                activeTransfer.DonorPartyId);
            plan.InitialSpawns[nextWaitingPartyId] = 1;
            plan.PriorityTransfers[nextWaitingPartyId] = transfer;
            plan.ConsumedPriorityTransferPartyIds.Remove(nextWaitingPartyId);
            plan.SettledPriorityTransferPartyIds.Remove(nextWaitingPartyId);

            Logger.Information("[TroopSupply] Reassigned priority slot {TransferId} in battle {MapEventId}: {DepartedPartyId} -> {WaitingPartyId}; donor remains {DonorPartyId}",
                transfer.TransferId, mapEventId, departedWaitingPartyId,
                nextWaitingPartyId, transfer.DonorPartyId);
            return true;
        }

        plan.InitialSpawns[activeTransfer.DonorPartyId] = donorInitialSpawns + 1;
        RetainVacantInitialSpawns(plan, activeTransfer.DonorPartyId, 1, replace: false);

        transfer = activeTransfer;
        released = true;
        Logger.Information("[TroopSupply] Released priority slot {TransferId} in battle {MapEventId}: restored donor {DonorPartyId} after {DepartedPartyId} left",
            transfer.TransferId, mapEventId, transfer.DonorPartyId, departedWaitingPartyId);
        return true;
    }

    public bool TryDeclinePrioritySlot(
        MapEvent mapEvent,
        long transferId,
        string waitingPartyId,
        out BattleInitialSpawnTransfer transfer,
        out bool released)
    {
        transfer = default;
        released = false;
        if (mapEvent == null || transferId <= 0 || string.IsNullOrEmpty(waitingPartyId)) return false;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return false;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan)
                || !plan.PriorityTransfers.TryGetValue(waitingPartyId, out var activeTransfer)
                || activeTransfer.TransferId != transferId
                || plan.ConsumedPriorityTransferPartyIds.Contains(waitingPartyId)
                || plan.SettledPriorityTransferPartyIds.Contains(waitingPartyId))
                return false;

            return TryReassignOrReleasePrioritySlot(
                mapEvent,
                mapEventId,
                plan,
                waitingPartyId,
                out transfer,
                out released);
        }
    }

    public bool CompletePrioritySpawn(MapEvent mapEvent, long transferId, string waitingPartyId)
    {
        if (mapEvent == null || transferId <= 0 || string.IsNullOrEmpty(waitingPartyId)) return false;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return false;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan)
                || !plan.PriorityTransfers.TryGetValue(waitingPartyId, out var activeTransfer)
                || activeTransfer.TransferId != transferId
                || plan.SettledPriorityTransferPartyIds.Contains(waitingPartyId))
                return false;

            if (plan.ConsumedPriorityTransferPartyIds.Add(waitingPartyId))
            {
                Logger.Information("[TroopSupply] Completed priority slot {TransferId} in battle {MapEventId} for party {WaitingPartyId}",
                    transferId, mapEventId, waitingPartyId);
            }
            return true;
        }
    }

    public IReadOnlyList<BattlePrioritySlotState> GetPrioritySlotSnapshot(MapEvent mapEvent)
    {
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
            return Array.Empty<BattlePrioritySlotState>();

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan))
                return Array.Empty<BattlePrioritySlotState>();

            var snapshot = new List<BattlePrioritySlotState>(
                plan.WaitingPlayerPartyIds.Count + plan.PriorityTransfers.Count);
            foreach (var waitingPartyId in plan.WaitingPlayerPartyIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                snapshot.Add(new BattlePrioritySlotState(
                    waitingPartyId,
                    transferId: 0,
                    donorPartyId: null,
                    isConsumed: false));
            }
            foreach (var transfer in plan.PriorityTransfers.Values
                         .Where(value => !plan.SettledPriorityTransferPartyIds.Contains(value.WaitingPartyId))
                         .OrderBy(value => value.TransferId)
                         .ThenBy(value => value.WaitingPartyId, StringComparer.Ordinal))
            {
                snapshot.Add(new BattlePrioritySlotState(
                    transfer.WaitingPartyId,
                    transfer.TransferId,
                    transfer.DonorPartyId,
                    plan.ConsumedPriorityTransferPartyIds.Contains(transfer.WaitingPartyId)));
            }
            return snapshot;
        }
    }

    public IReadOnlyList<SideReserve> GetOwnedReserves(MapEvent mapEvent, string controllerId, bool isHost,
        IReadOnlyCollection<string> absentControllers = null)
    {
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
            return Array.Empty<SideReserve>();

        EnsureBuilt(mapEvent, mapEventId);

        var attacker = new List<PartyReserve>();
        var defender = new List<PartyReserve>();

        foreach (var party in EnumerateParties(mapEvent))
        {
            if (!objectManager.TryGetId(party, out var partyId))
                continue;

            // Who fields this party: its own player; or — for an AI party in a player-led army — that army
            // leader (#3 "army leader deploys the army"); or, when no player does (including a player that
            // DROPPED from this battle and hasn't returned), the host.
            TryGetOwningPlayer(party, out var partyOwnerController);
            TryGetArmyLeaderPlayer(party, out var armyLeaderController);
            var owningController = ResolveOwningController(partyOwnerController, armyLeaderController, absentControllers);
            if (!IsOwnedByRequester(owningController, controllerId, isHost))
                continue;

            if (!ledger.TryGetReserve(mapEventId, partyId, out var entries, out var supplied))
                continue;

            var entriesArray = new TroopReserveEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++) entriesArray[i] = entries[i];
            var departedSeeds = ledger.GetDepartedSeeds(mapEventId, partyId).ToArray();

            int initialSpawnCount = GetInitialSpawnCount(mapEventId, partyId, entriesArray.Length);
            var reserve = new PartyReserve(
                partyId,
                supplied,
                entriesArray,
                initialSpawnCount,
                departedSeeds);
            if ((party.Party?.Side ?? BattleSideEnum.None) == BattleSideEnum.Attacker)
                attacker.Add(reserve);
            else
                defender.Add(reserve);
        }

        Logger.Information("[TroopSupply] Owned reserves for {Controller} (isHost={IsHost}): Attacker={AtkParties}p/{AtkEntries}e, Defender={DefParties}p/{DefEntries}e",
            controllerId, isHost, attacker.Count, CountEntries(attacker), defender.Count, CountEntries(defender));

        // Return both sides (empty parties = "owns nothing here") so every supplier becomes populated.
        return new[]
        {
            new SideReserve(BattleSideEnum.Attacker, attacker.ToArray()),
            new SideReserve(BattleSideEnum.Defender, defender.ToArray()),
        };
    }

    public void ForgetController(MapEvent mapEvent, string controllerId)
    {
        if (mapEvent == null || string.IsNullOrEmpty(controllerId)) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        lock (gate)
        {
            foreach (var party in EnumerateParties(mapEvent))
            {
                if (!objectManager.TryGetId(party, out var partyId)) continue;
                if (!TryGetOwningPlayer(party, out var ownerControllerId) || ownerControllerId != controllerId) continue;

                ledger.RemoveParty(mapEventId, partyId);
                builtParties.Remove(partyId);
                if (spawnPlans.TryGetValue(mapEventId, out var plan))
                    plan.ForgottenPlayerPartyIds.Add(partyId);
                Logger.Information("[TroopSupply] Forgot party {PartyId} of retreating {Controller} (re-flattens fresh on rejoin)",
                    partyId, controllerId);
            }
        }
    }

    public void RebuildMapEventReserves(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        lock (gate)
        {
            var livePartyIds = new HashSet<string>();
            foreach (var party in EnumerateParties(mapEvent))
            {
                if (!objectManager.TryGetId(party, out var partyId)) continue;
                livePartyIds.Add(partyId);
                if (ledger.TryGetReserve(mapEventId, partyId, out _, out _))
                {
                    ledger.ResetSupplied(mapEventId, partyId);
                    builtParties.Add(partyId);
                }
                else
                {
                    builtParties.Remove(partyId);
                }
            }

            foreach (var partyId in ledger.GetParties(mapEventId))
            {
                if (livePartyIds.Contains(partyId)) continue;
                ledger.RemoveParty(mapEventId, partyId);
                builtParties.Remove(partyId);
            }

            EnsureBuilt(mapEvent, mapEventId);
            Logger.Information("[TroopSupply] Reset ALL live reserves of battle {MapEventId} while preserving frozen troop identities, departures, and spawn plan ({Count} parties)",
                mapEventId, livePartyIds.Count);
        }
    }

    public void ForgetMapEvent(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        lock (gate)
        {
            int forgotten = 0;
            foreach (var party in EnumerateParties(mapEvent))
                if (objectManager.TryGetId(party, out var partyId) && builtParties.Remove(partyId))
                    forgotten++;

            // Drop the whole battle's reserves in one shot — covers every party (including any no longer
            // enumerable) and leaves no empty per-battle entry behind, so a restart re-flattens fresh.
            ledger.Remove(mapEventId);
            spawnPlans.Remove(mapEventId);
            Logger.Information("[TroopSupply] Forgot ALL reserves of battle {MapEventId} ({Count} flatten-cache entries cleared)",
                mapEventId, forgotten);
        }
    }

    private int GetInitialSpawnCount(string mapEventId, string partyId, int capacity)
    {
        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan))
                return 0;
            if (!plan.InitialSpawns.TryGetValue(partyId, out var initialSpawnCount))
                return 0;

            return Math.Min(initialSpawnCount, Math.Max(0, capacity));
        }
    }

    private static int GetOrGrantInitialSpawns(BattleSpawnPlan plan, BattleInitialSpawnParty party)
    {
        if (plan.InitialSpawns.TryGetValue(party.PartyId, out var existing))
            return Math.Min(existing, party.Capacity);

        int unassigned = Math.Max(0, plan.BattleSize - plan.AssignedCount);
        int granted = Math.Min(party.Capacity, unassigned);
        plan.Parties[party.PartyId] = party;
        plan.InitialSpawns[party.PartyId] = granted;
        plan.AssignedCount += granted;
        return granted;
    }

    private static void QueueWaitingPlayerParty(BattleSpawnPlan plan, string partyId)
    {
        if (!plan.WaitingPlayerPartyIds.Add(partyId))
            return;

        plan.WaitingPlayerParties.Enqueue(partyId);
    }

    private static void RemoveWaitingPlayerParty(BattleSpawnPlan plan, string partyId)
    {
        int queuedParties = plan.WaitingPlayerParties.Count;
        for (int i = 0; i < queuedParties; i++)
        {
            var candidate = plan.WaitingPlayerParties.Dequeue();
            if (!string.Equals(candidate, partyId, StringComparison.Ordinal))
                plan.WaitingPlayerParties.Enqueue(candidate);
        }
    }

    public int RetainInitialSpawnVacancies(MapEvent mapEvent, string donorPartyId)
    {
        if (mapEvent == null || string.IsNullOrEmpty(donorPartyId)) return 0;
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return 0;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan)
                || !plan.Parties.ContainsKey(donorPartyId))
            {
                return 0;
            }

            plan.ForgottenPlayerPartyIds.Add(donorPartyId);
            if (!plan.InitialSpawns.TryGetValue(donorPartyId, out var initialSpawns)
                || initialSpawns <= 0)
            {
                return 0;
            }

            int retained = RetainVacantInitialSpawns(
                plan,
                donorPartyId,
                initialSpawns,
                replace: true);

            Logger.Information("[TroopSupply] Retained {Count} initial vacancies in battle {MapEventId} for departing party {DonorPartyId}",
                retained, mapEventId, donorPartyId);
            return retained;
        }
    }

    public bool TryTransferRetainedInitialSpawn(
        MapEvent mapEvent,
        out BattleInitialSpawnTransfer transfer,
        out BattleInitialSpawnTransfer settledTransfer)
    {
        transfer = default;
        settledTransfer = default;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
            return false;

        lock (gate)
        {
            if (!spawnPlans.TryGetValue(mapEventId, out var plan)
                || !TryPeekVacantDonor(plan, out var donorPartyId)
                || !TryTakeNextWaitingPlayerParty(
                    mapEvent,
                    mapEventId,
                    plan,
                    out var waitingPartyId))
            {
                return false;
            }

            plan.VacantDonorParties.Dequeue();
            int remainingVacancies = plan.VacantInitialSpawns[donorPartyId] - 1;
            if (remainingVacancies > 0)
            {
                plan.VacantInitialSpawns[donorPartyId] = remainingVacancies;
                plan.VacantDonorParties.Enqueue(donorPartyId);
            }
            else
            {
                plan.VacantInitialSpawns.Remove(donorPartyId);
            }

            transfer = AssignPrioritySlot(
                plan,
                waitingPartyId,
                donorPartyId,
                out settledTransfer);

            Logger.Information("[TroopSupply] Transferred retained initial slot {TransferId} in battle {MapEventId}: {DonorPartyId} -> {WaitingPartyId}",
                transfer.TransferId, mapEventId, donorPartyId, waitingPartyId);
            return true;
        }
    }

    private bool TryTakeNextWaitingPlayerParty(
        MapEvent mapEvent,
        string mapEventId,
        BattleSpawnPlan plan,
        out string waitingPartyId)
    {
        waitingPartyId = null;
        while (plan.WaitingPlayerParties.Count > 0)
        {
            var candidate = plan.WaitingPlayerParties.Dequeue();
            if (!plan.WaitingPlayerPartyIds.Remove(candidate))
                continue;
            if (!IsLiveWaitingPlayerParty(mapEvent, mapEventId, plan, candidate))
                continue;

            waitingPartyId = candidate;
            return true;
        }

        return false;
    }

    private static bool TryPeekVacantDonor(BattleSpawnPlan plan, out string donorPartyId)
    {
        donorPartyId = null;
        while (plan.VacantDonorParties.Count > 0)
        {
            var candidate = plan.VacantDonorParties.Peek();
            if (!plan.VacantInitialSpawns.TryGetValue(candidate, out var vacancies)
                || vacancies <= 0
                || !plan.InitialSpawns.TryGetValue(candidate, out var initialSpawns)
                || initialSpawns <= 0)
            {
                plan.VacantDonorParties.Dequeue();
                plan.VacantInitialSpawns.Remove(candidate);
                continue;
            }

            if (vacancies > initialSpawns)
                plan.VacantInitialSpawns[candidate] = initialSpawns;

            donorPartyId = candidate;
            return true;
        }

        return false;
    }

    private static BattleInitialSpawnTransfer AssignPrioritySlot(
        BattleSpawnPlan plan,
        string waitingPartyId,
        string donorPartyId,
        out BattleInitialSpawnTransfer settledTransfer)
    {
        settledTransfer = default;
        int donorInitialSpawns = plan.InitialSpawns[donorPartyId];
        plan.InitialSpawns[donorPartyId] = donorInitialSpawns - 1;
        plan.InitialSpawns[waitingPartyId] = 1;
        var transfer = new BattleInitialSpawnTransfer(
            ++plan.LastTransferId,
            waitingPartyId,
            donorPartyId);

        if (plan.PriorityTransfers.TryGetValue(donorPartyId, out var previousTransfer)
            && (plan.ConsumedPriorityTransferPartyIds.Contains(donorPartyId)
                || plan.SettledPriorityTransferPartyIds.Contains(donorPartyId)))
        {
            if (!plan.SettledPriorityTransferPartyIds.Contains(donorPartyId))
                settledTransfer = previousTransfer;
            plan.PriorityTransfers.Remove(donorPartyId);
            plan.ConsumedPriorityTransferPartyIds.Remove(donorPartyId);
            plan.SettledPriorityTransferPartyIds.Remove(donorPartyId);
        }

        plan.PriorityTransfers[waitingPartyId] = transfer;
        plan.ConsumedPriorityTransferPartyIds.Remove(waitingPartyId);
        plan.SettledPriorityTransferPartyIds.Remove(waitingPartyId);
        ClampRetainedInitialSpawns(plan, donorPartyId);
        return transfer;
    }

    private static int RetainVacantInitialSpawns(
        BattleSpawnPlan plan,
        string donorPartyId,
        int count,
        bool replace)
    {
        if (!plan.InitialSpawns.TryGetValue(donorPartyId, out var initialSpawns)
            || initialSpawns <= 0
            || count <= 0)
        {
            ClearRetainedInitialSpawns(plan, donorPartyId);
            return 0;
        }

        plan.VacantInitialSpawns.TryGetValue(donorPartyId, out var existing);
        existing = Math.Min(existing, initialSpawns);
        int retained = replace
            ? Math.Max(existing, Math.Min(count, initialSpawns))
            : Math.Min(initialSpawns, existing + count);
        if (existing == 0 && retained > 0)
            plan.VacantDonorParties.Enqueue(donorPartyId);
        plan.VacantInitialSpawns[donorPartyId] = retained;
        return retained;
    }

    private static void ClampRetainedInitialSpawns(BattleSpawnPlan plan, string donorPartyId)
    {
        if (!plan.VacantInitialSpawns.TryGetValue(donorPartyId, out var vacancies))
            return;

        if (!plan.InitialSpawns.TryGetValue(donorPartyId, out var initialSpawns)
            || initialSpawns <= 0)
        {
            ClearRetainedInitialSpawns(plan, donorPartyId);
            return;
        }

        if (vacancies > initialSpawns)
            plan.VacantInitialSpawns[donorPartyId] = initialSpawns;
    }

    private static void ClearRetainedInitialSpawns(BattleSpawnPlan plan, string donorPartyId)
    {
        plan.VacantInitialSpawns.Remove(donorPartyId);
    }

    private bool IsLiveWaitingPlayerParty(
        MapEvent mapEvent,
        string mapEventId,
        BattleSpawnPlan plan,
        string waitingPartyId)
    {
        if (!plan.Parties.TryGetValue(waitingPartyId, out var waitingParty)
            || plan.ForgottenPlayerPartyIds.Contains(waitingPartyId)
            || !waitingParty.IsDirectPlayerParty
            || waitingParty.Capacity <= 0
            || !plan.InitialSpawns.TryGetValue(waitingPartyId, out var initialSpawns)
            || initialSpawns != 0)
            return false;

        MapEventParty liveParty = null;
        foreach (var party in EnumerateParties(mapEvent))
        {
            if (objectManager.TryGetId(party, out var partyId) && partyId == waitingPartyId)
            {
                liveParty = party;
                break;
            }
        }

        if (liveParty == null
            || (liveParty.Party?.Side ?? BattleSideEnum.None) != waitingParty.Side
            || !TryGetOwningPlayer(liveParty, out var controllerId)
            || !string.Equals(controllerId, waitingParty.AuthorityId, StringComparison.Ordinal))
            return false;

        return ledger.TryGetReserve(mapEventId, waitingPartyId, out var entries, out _)
            && entries.Count > 0;
    }

    private static int CountEntries(List<PartyReserve> parties)
    {
        int total = 0;
        foreach (var party in parties) total += party.Entries.Length;
        return total;
    }

    private void EnsureBuilt(MapEvent mapEvent, string mapEventId)
    {
        lock (gate)
        {
            // Flatten every party not yet in the ledger. Re-scanned on each reserve build so a mid-battle
            // joiner's party (added after the initial build) is picked up rather than left out.
            foreach (var party in EnumerateParties(mapEvent))
            {
                if (!objectManager.TryGetId(party, out var partyId))
                    continue;

                if (!builtParties.Add(partyId))
                    continue; // already flattened

                bool hadRoster = party._roster != null;
                TryGetOwningPlayer(party, out _, out var directPlayerCharacterId);
                var entries = FlattenParty(party, directPlayerCharacterId);
                ledger.SetReserve(mapEventId, partyId, entries);
                Logger.Information("[TroopSupply] Built reserve: party {PartyId} side {Side} -> {Count} troops (roster was {Roster})",
                    partyId, party.Party?.Side, entries.Count, hadRoster ? "present" : "null");
            }
        }
    }

    // The server's MapEventParty._roster is the flattened roster; its descriptors are the authoritative, stable
    // seeds we hand out (and what the casualty path keys on). An enemy/AI party that was never made
    // mission-ready can have a null _roster, so flatten it here (server-side Update is allowed).
    private List<TroopReserveEntry> FlattenParty(MapEventParty party, string directPlayerCharacterId)
    {
        var entries = new List<TroopReserveEntry>();
        TroopReserveEntry? directPlayerEntry = null;

        if (party._roster == null)
            party.Update();

        var roster = party._roster;
        if (roster == null)
            return entries;

        foreach (var element in roster)
        {
            if (element.IsWounded || element.IsRouted || element.IsKilled)
                continue;

            var character = element.Troop;
            if (character == null)
                continue;

            // Heroes and regular troops alike are keyed by their CharacterObject id (hero CharacterObjects are
            // registered too — CharacterObjectRegistry), so resolve it uniformly.
            if (!objectManager.TryGetId(character, out var characterId))
            {
                Logger.Warning("[TroopSupply] Skipped troop {Char} (CharacterObject unresolvable on server)", character.StringId);
                continue;
            }

            var entry = new TroopReserveEntry(
                element.Descriptor.UniqueSeed, characterId, (int)character.GetFormationClass());
            if (directPlayerEntry == null
                && !string.IsNullOrEmpty(directPlayerCharacterId)
                && characterId == directPlayerCharacterId)
            {
                directPlayerEntry = entry;
            }
            else
            {
                entries.Add(entry);
            }
        }

        if (directPlayerEntry != null)
            entries.Insert(0, directPlayerEntry.Value);

        return entries;
    }

    /// <summary>
    /// The controller that owns a party's reserve, or null if no present player does (so the host fields it).
    /// A party's own player wins; an AI party (no own player) in a player-led army falls to that army leader.
    /// An owner in <paramref name="absentControllers"/> (dropped from the battle, not yet returned) resolves
    /// to null: its parties fall to the host until it re-enters, at which point the caller re-issues both
    /// scopes (the returner's grant and the host's shrunk refresh).
    /// </summary>
    internal static string ResolveOwningController(string partyOwnerController, string armyLeaderController,
        IReadOnlyCollection<string> absentControllers = null)
    {
        var owner = partyOwnerController ?? armyLeaderController;
        if (owner != null && absentControllers != null && absentControllers.Contains(owner))
            return null;
        return owner;
    }

    /// <summary>
    /// Whether <paramref name="requesterController"/> fields the party: it is the owning controller, or — when
    /// no player owns it — the requester is the host.
    /// </summary>
    internal static bool IsOwnedByRequester(string owningController, string requesterController, bool requesterIsHost)
        => owningController != null ? owningController == requesterController : requesterIsHost;

    private bool TryGetOwningPlayer(MapEventParty party, out string controllerId)
        => TryGetOwningPlayer(party, out controllerId, out _);

    private bool TryGetOwningPlayer(
        MapEventParty party,
        out string controllerId,
        out string characterObjectId)
    {
        controllerId = null;
        characterObjectId = null;
        var mobileParty = party.Party?.MobileParty;
        return mobileParty != null
            && TryGetPlayerController(mobileParty, out controllerId, out characterObjectId);
    }

    // The controller of the player who LEADS this party's army, if the army's leader party is a player's. Null
    // when the party is not in an army, or the army is led by an AI lord.
    private bool TryGetArmyLeaderPlayer(MapEventParty party, out string controllerId)
    {
        controllerId = null;
        var leaderMobileParty = party.Party?.MobileParty?.Army?.LeaderParty;
        return leaderMobileParty != null && TryGetPlayerController(leaderMobileParty, out controllerId);
    }

    // The controller of the connected player whose party this is, if any.
    private bool TryGetPlayerController(MobileParty mobileParty, out string controllerId)
        => TryGetPlayerController(mobileParty, out controllerId, out _);

    private bool TryGetPlayerController(
        MobileParty mobileParty,
        out string controllerId,
        out string characterObjectId)
    {
        controllerId = null;
        characterObjectId = null;
        if (!objectManager.TryGetId(mobileParty, out var mobilePartyId))
            return false;

        foreach (var player in playerManager.Players)
        {
            if (player.MobilePartyId == mobilePartyId)
            {
                controllerId = player.ControllerId;
                characterObjectId = player.CharacterObjectId;
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<MapEventParty> EnumerateParties(MapEvent mapEvent)
    {
        foreach (var party in mapEvent.AttackerSide.Parties)
            yield return party;
        foreach (var party in mapEvent.DefenderSide.Parties)
            yield return party;
    }
}
