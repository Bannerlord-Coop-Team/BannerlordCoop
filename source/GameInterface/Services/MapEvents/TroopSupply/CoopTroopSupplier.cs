using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// A coop battle's troop supplier for one side: instead of pulling from the local <c>MapEventSide</c> pool
/// (as the native <c>PartyGroupTroopSupplier</c> does), it serves troops the SERVER committed — fed in over
/// the network as <see cref="PartyReserve"/>s. The native deployment/reinforcement/formation logic drives it
/// exactly as it drives the native supplier, so we only change where troops come from. Because the server
/// owns the descriptor seeds, every client agrees on troop identity, and on disconnect/migration a fresh
/// owner resumes from the server's supplied pointer.
/// <para>
/// State is per party (the side aggregates one or more), so the supplied pointer maps cleanly to the server's
/// per-party ledger and to migration. Substituted into the mission by <c>BattleTroopSupplierInjectionPatch</c>
/// and fed via <c>CoopTroopSupplierRegistry</c>. Supply runs on the game thread, <see cref="SetReserve"/> on
/// the network thread — hence the lock.
/// </para>
/// </summary>
public class CoopTroopSupplier : IMissionTroopSupplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopTroopSupplier>();
    private static long nextSupplierLockOrder;

    public enum ClaimedTroopUseResult
    {
        ClaimMissing,
        Deferred,
        Committed,
    }

    public readonly struct PartyCapacitySnapshot
    {
        public readonly string PartyId;
        public readonly int TotalTroops;
        public readonly int InitialSpawnCount;

        public PartyCapacitySnapshot(string partyId, int totalTroops, int initialSpawnCount)
        {
            PartyId = partyId;
            TotalTroops = totalTroops;
            InitialSpawnCount = initialSpawnCount;
        }
    }

    /// <summary>One lock-consistent view of the fields used to size a battle side.</summary>
    public readonly struct SizingSnapshot
    {
        public readonly bool IsPopulated;
        public readonly int TotalTroops;
        public readonly int InitialTroops;
        public readonly int ReserveRevision;
        public readonly long GrantGeneration;
        public readonly bool CompletesInitialSizing;
        public readonly PartyCapacitySnapshot[] PartyCapacities;

        public SizingSnapshot(
            bool isPopulated,
            int totalTroops,
            int initialTroops,
            int reserveRevision,
            long grantGeneration,
            bool completesInitialSizing,
            PartyCapacitySnapshot[] partyCapacities)
        {
            IsPopulated = isPopulated;
            TotalTroops = totalTroops;
            InitialTroops = initialTroops;
            ReserveRevision = reserveRevision;
            GrantGeneration = grantGeneration;
            CompletesInitialSizing = completesInitialSizing;
            PartyCapacities = partyCapacities;
        }
    }

    /// <summary>The current tail of parties captured by the native initial phase.</summary>
    public readonly struct InitialPhaseSnapshot
    {
        public readonly bool IsCaptured;
        public readonly int ReserveRevision;
        public readonly int RemainingTroops;
        public readonly int RemainingInitialTroops;

        public InitialPhaseSnapshot(
            bool isCaptured,
            int reserveRevision,
            int remainingTroops,
            int remainingInitialTroops)
        {
            IsCaptured = isCaptured;
            ReserveRevision = reserveRevision;
            RemainingTroops = remainingTroops;
            RemainingInitialTroops = remainingInitialTroops;
        }
    }

    private sealed class PartyState
    {
        public string PartyId;
        public TroopReserveEntry[] Entries = Array.Empty<TroopReserveEntry>();
        public int Supplied;
        public int InitialSpawnCount;
        public HashSet<int> DepartedSeeds = new HashSet<int>();
        public int ClaimBaseSupplied;
        public HashSet<int> PendingClaimSeeds;
        public bool ParkClaimsAtEnd;
    }

    private readonly object gate = new object();
    private readonly long supplierLockOrder = Interlocked.Increment(ref nextSupplierLockOrder);
    private readonly List<PartyState> parties = new List<PartyState>();
    private readonly Dictionary<string, int> representedPhaseCapacities = new Dictionary<string, int>();
    private readonly Dictionary<string, int> initialSupplyRemaining = new Dictionary<string, int>();
    private readonly HashSet<string> initialPhasePartyIds = new HashSet<string>();
    private bool initialPhaseCaptured;
    private bool initialSupplyActive;
    // seed -> partyId, rebuilt alongside `parties` in SetReserve, so GetParty/FindPartyId is O(1) instead of
    // scanning every party's entries per agent. Entry seeds are server-unique, so one seed maps to one party.
    private readonly Dictionary<int, string> seedToPartyId = new Dictionary<int, string>();
    private bool populated;
    private int reserveRevision;
    private long grantGeneration;
    private bool completesInitialSizing;
    private int numWounded, numKilled, numRouted;
    // Injected at construction (a stable per-session singleton) so the per-agent supply path resolves troop/party
    // objects without hitting the service locator each call. Null only in tests that don't exercise that path.
    private readonly IObjectManager objectManager;

    public string MapEventId { get; }
    public BattleSideEnum Side { get; }

    public CoopTroopSupplier(string mapEventId, BattleSideEnum side, IObjectManager objectManager)
    {
        MapEventId = mapEventId;
        Side = side;
        this.objectManager = objectManager;
    }

    /// <summary>
    /// [Network thread] Replace this side's reserve with the server's authoritative set (each party with its
    /// current supplied pointer — 0 at battle start, the server's pointer on migration). Marks us populated,
    /// so a side this client owns nothing on (empty set) reports "done" instead of blocking deployment.
    /// A party's pointer never rewinds: if we have already supplied further than a (possibly stale) resend
    /// carries, we keep our local pointer — see the monotonic resume below.
    /// <para>
    /// Returns the FINAL local supplied pointers of the parties this REPLACE dropped (held before, absent
    /// from the new set) — the BR-033 flush payload. Captured under the same lock as the replace itself, so
    /// no supply can advance a dropped party between the capture and the removal: the returned pointers are
    /// definitively this supplier's last word on those parties.
    /// </para>
    /// </summary>
    public IReadOnlyList<(string PartyId, int Supplied)> SetReserve(
        IReadOnlyList<PartyReserve> reserve,
        long grantGeneration = 0,
        bool completesInitialSizing = true)
    {
        var dropped = new List<(string PartyId, int Supplied)>();
        lock (gate)
        {
            // Capture the current per-party pointers before rebuilding. A resend can carry a STALE pointer: the
            // server's ledger lags our local supply by up to one report interval, and on migration it re-sends
            // our OWN party at that lagging value. Resuming from the server value alone would rewind a party we
            // have already supplied further and re-spawn troops already on the field (with duplicate seeds). So
            // resume from max(local, server), mirroring the server ledger's own monotonic ReportSupplied.
            var priorStates = new Dictionary<string, PartyState>(parties.Count);
            foreach (var existing in parties)
                priorStates[existing.PartyId] = existing;

            parties.Clear();
            seedToPartyId.Clear();
            if (reserve != null)
            {
                foreach (var party in reserve)
                {
                    var entries = party.Entries ?? Array.Empty<TroopReserveEntry>();
                    int supplied = Math.Min(Math.Max(0, party.SuppliedCount), entries.Length);
                    priorStates.TryGetValue(party.PartyId, out var priorState);
                    if (priorState != null && priorState.Supplied > supplied)
                        supplied = Math.Min(priorState.Supplied, entries.Length);
                    priorStates.Remove(party.PartyId); // kept — not part of the dropped set
                    int initialSpawnCount = Math.Min(Math.Max(0, party.InitialSpawnCount), entries.Length);
                    var departedSeeds = new HashSet<int>(party.DepartedSeeds ?? Array.Empty<int>());
                    var currentSeeds = new HashSet<int>();
                    foreach (var entry in entries)
                        currentSeeds.Add(entry.Seed);
                    departedSeeds.RemoveWhere(seed => !currentSeeds.Contains(seed));
                    if (priorState != null)
                        foreach (var seed in priorState.DepartedSeeds)
                            if (currentSeeds.Contains(seed))
                                departedSeeds.Add(seed);
                    if (priorState != null
                        && supplied > priorState.Supplied
                        && initialSupplyRemaining.TryGetValue(party.PartyId, out var remainingInitial))
                    {
                        int consumedAvailable = 0;
                        for (int i = priorState.Supplied; i < supplied; i++)
                            if (!departedSeeds.Contains(entries[i].Seed))
                                consumedAvailable++;
                        int adjustedInitial = Math.Max(0, remainingInitial - consumedAvailable);
                        if (adjustedInitial == 0)
                            initialSupplyRemaining.Remove(party.PartyId);
                        else
                            initialSupplyRemaining[party.PartyId] = adjustedInitial;
                    }
                    var state = new PartyState
                    {
                        PartyId = party.PartyId,
                        Entries = entries,
                        Supplied = supplied,
                        InitialSpawnCount = initialSpawnCount,
                        DepartedSeeds = departedSeeds,
                    };
                    PreservePendingClaims(priorState, state, party.SuppliedCount);
                    parties.Add(state);
                    foreach (var entry in entries)
                        seedToPartyId[entry.Seed] = party.PartyId;
                }
            }
            populated = true;
            reserveRevision++;
            this.grantGeneration = grantGeneration;
            this.completesInitialSizing = completesInitialSizing;

            // Whatever the new set did not re-claim was DROPPED by this replace.
            foreach (var prior in priorStates)
                dropped.Add((prior.Key, GetReportableSupplied(prior.Value)));
        }

        Logger.Information("[TroopSupply] Supplier {MapEvent} side {Side}: SetReserve generation {Generation} (complete={Complete}), {Parties} parties / {Entries} troops / {Initial} initial slots ({Dropped} parties dropped)",
            MapEventId, Side, grantGeneration, completesInitialSizing, parties.Count, NumTroopsNotSupplied, InitialTroops, dropped.Count);
        return dropped;
    }

    /// <summary>How many troops have been supplied per party — reported back to the server for the ledger.</summary>
    public IReadOnlyList<(string partyId, int supplied)> GetSuppliedByParty()
    {
        lock (gate)
        {
            var result = new List<(string, int)>(parties.Count);
            foreach (var party in parties)
                result.Add((party.PartyId, GetReportableSupplied(party)));
            return result;
        }
    }

    public int NumRemovedTroops { get { lock (gate) { return numWounded + numKilled + numRouted; } } }

    /// <summary>Whether the server's reserve has arrived (counts/identity known and final).</summary>
    public bool IsPopulated { get { lock (gate) { return populated; } } }

    /// <summary>Monotonic count of authoritative reserve snapshots applied to this supplier.</summary>
    public int ReserveRevision { get { lock (gate) { return reserveRevision; } } }

    /// <summary>The server grant generation represented by this supplier's current reserve.</summary>
    public long GrantGeneration { get { lock (gate) { return grantGeneration; } } }

    /// <summary>Whether the current grant explicitly finalized both sides for initial mission sizing.</summary>
    public bool CompletesInitialSizing { get { lock (gate) { return completesInitialSizing; } } }

    /// <summary>Atomically reads the populated state, reserve totals, entitlement, and revision.</summary>
    public SizingSnapshot GetSizingSnapshot()
    {
        lock (gate)
            return CreateSizingSnapshot();
    }

    /// <summary>Atomically reads both battle sides so neither reserve can change during joint sizing.</summary>
    public static void GetSizingSnapshots(
        CoopTroopSupplier defenderSupplier,
        CoopTroopSupplier attackerSupplier,
        out SizingSnapshot defender,
        out SizingSnapshot attacker)
    {
        if (defenderSupplier == null) throw new ArgumentNullException(nameof(defenderSupplier));
        if (attackerSupplier == null) throw new ArgumentNullException(nameof(attackerSupplier));

        if (ReferenceEquals(defenderSupplier, attackerSupplier))
        {
            defender = defenderSupplier.GetSizingSnapshot();
            attacker = defender;
            return;
        }

        var (first, second) = OrderSizingLocks(defenderSupplier, attackerSupplier);

        lock (first.gate)
        lock (second.gate)
        {
            defender = defenderSupplier.CreateSizingSnapshot();
            attacker = attackerSupplier.CreateSizingSnapshot();
        }
    }

    internal static (CoopTroopSupplier First, CoopTroopSupplier Second) OrderSizingLocks(
        CoopTroopSupplier left,
        CoopTroopSupplier right)
    {
        return left.supplierLockOrder < right.supplierLockOrder
            ? (left, right)
            : (right, left);
    }

    private SizingSnapshot CreateSizingSnapshot()
    {
        int totalTroops = 0;
        int initialTroops = 0;
        var partyCapacities = new PartyCapacitySnapshot[parties.Count];
        int partyIndex = 0;
        foreach (var party in parties)
        {
            int remainingTroops = CountAvailableRemaining(party);
            int initialSpawnCount = CountAvailableInitialRemaining(party);
            totalTroops += remainingTroops;
            initialTroops += initialSpawnCount;
            partyCapacities[partyIndex++] = new PartyCapacitySnapshot(
                party.PartyId,
                party.Entries.Length,
                initialSpawnCount);
        }

        return new SizingSnapshot(
            populated,
            totalTroops,
            initialTroops,
            reserveRevision,
            grantGeneration,
            completesInitialSizing,
            partyCapacities);
    }

    /// <summary>Record the exact party depths represented by the native mission phase.</summary>
    public void RecordPhaseCapacities(IReadOnlyList<PartyCapacitySnapshot> capacities)
    {
        if (capacities == null) return;

        lock (gate)
        {
            foreach (var capacity in capacities)
                RecordPhaseCapacityInternal(capacity.PartyId, capacity.TotalTroops);
        }
    }

    /// <summary>Capture the frozen per-party leases used by the native initial deployment pull.</summary>
    public void BeginInitialSupply(IReadOnlyList<PartyCapacitySnapshot> capacities)
    {
        lock (gate)
        {
            initialSupplyRemaining.Clear();
            initialPhasePartyIds.Clear();
            initialPhaseCaptured = capacities != null;
            initialSupplyActive = false;
            if (capacities == null) return;

            foreach (var capacity in capacities)
            {
                initialPhasePartyIds.Add(capacity.PartyId);
                int available = GetRemainingInitialForPartyInternal(capacity.PartyId);
                int initial = Math.Min(Math.Max(0, capacity.InitialSpawnCount), available);
                if (initial > 0)
                    initialSupplyRemaining[capacity.PartyId] = initial;
            }
            initialSupplyActive = initialSupplyRemaining.Count > 0;
        }
    }

    /// <summary>Read the current unsupplied tail of the parties represented by the initial native phase.</summary>
    public InitialPhaseSnapshot GetInitialPhaseSnapshot()
    {
        lock (gate)
        {
            int remainingTroops = 0;
            int remainingInitialTroops = 0;
            foreach (var party in parties)
            {
                if (!initialPhasePartyIds.Contains(party.PartyId)) continue;

                int remaining = CountAvailableRemaining(party);
                remainingTroops += remaining;
                if (initialSupplyRemaining.TryGetValue(party.PartyId, out var initial))
                    remainingInitialTroops += Math.Min(
                        Math.Max(0, initial),
                        CountAvailableInitialRemaining(party));
            }

            return new InitialPhaseSnapshot(
                initialPhaseCaptured,
                reserveRevision,
                remainingTroops,
                remainingInitialTroops);
        }
    }

    /// <summary>Commit one game-thread phase rebase and discard captured leases no longer in this reserve.</summary>
    public bool CommitInitialPhaseRebase(int expectedReserveRevision)
    {
        lock (gate)
        {
            if (reserveRevision != expectedReserveRevision) return false;

            var capturedPartyIds = new List<string>(initialSupplyRemaining.Keys);
            foreach (var partyId in capturedPartyIds)
            {
                int available = GetRemainingInitialForPartyInternal(partyId);
                if (available == 0)
                    initialSupplyRemaining.Remove(partyId);
                else if (initialSupplyRemaining[partyId] > available)
                    initialSupplyRemaining[partyId] = available;
            }

            initialSupplyActive = initialSupplyRemaining.Count > 0;
            return true;
        }
    }

    /// <summary>Snapshot the exact seeds in one party's current unsupplied tail.</summary>
    public IReadOnlyList<int> GetRemainingSeedsForParty(string partyId)
    {
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId) continue;

                int count = Math.Max(0, party.Entries.Length - party.Supplied);
                var seeds = new int[count];
                for (int i = 0; i < count; i++)
                    seeds[i] = party.Entries[party.Supplied + i].Seed;
                return seeds;
            }

            return Array.Empty<int>();
        }
    }

    /// <summary>Consume the contiguous unsupplied prefix already represented by live or departed agents.</summary>
    public int AdvanceResolvedPrefix(string partyId, ISet<int> resolvedSeeds)
    {
        if (resolvedSeeds == null || resolvedSeeds.Count == 0) return 0;

        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId || party.PendingClaimSeeds != null) continue;

                int start = party.Supplied;
                int resolvedAvailable = 0;
                while (party.Supplied < party.Entries.Length
                    && resolvedSeeds.Contains(party.Entries[party.Supplied].Seed))
                {
                    if (party.Supplied < party.InitialSpawnCount
                        && !party.DepartedSeeds.Contains(party.Entries[party.Supplied].Seed))
                        resolvedAvailable++;
                    party.Supplied++;
                }

                int advanced = party.Supplied - start;
                if (advanced > 0 && initialSupplyRemaining.TryGetValue(partyId, out var initial))
                {
                    int remainingInitial = Math.Max(0, initial - resolvedAvailable);
                    if (remainingInitial == 0)
                        initialSupplyRemaining.Remove(partyId);
                    else
                        initialSupplyRemaining[partyId] = remainingInitial;
                    if (initialSupplyRemaining.Count == 0)
                        initialSupplyActive = false;
                }
                return advanced;
            }

            return 0;
        }
    }

    /// <summary>Record one post-plan party's depth after extending the native mission phase.</summary>
    public void RecordPhaseCapacity(string partyId, int totalTroops)
    {
        if (string.IsNullOrEmpty(partyId)) return;

        lock (gate)
            RecordPhaseCapacityInternal(partyId, totalTroops);
    }

    /// <summary>Historical depth already represented in the native phase, retained across scope shrink.</summary>
    public int GetRepresentedPhaseCapacity(string partyId)
    {
        lock (gate)
            return representedPhaseCapacities.TryGetValue(partyId, out var capacity) ? capacity : 0;
    }

    private void RecordPhaseCapacityInternal(string partyId, int totalTroops)
    {
        if (string.IsNullOrEmpty(partyId)) return;

        int capacity = Math.Max(0, totalTroops);
        if (!representedPhaseCapacities.TryGetValue(partyId, out var current) || capacity > current)
            representedPhaseCapacities[partyId] = capacity;
    }

    /// <summary>Remaining troop count for each party in the current authoritative reserve.</summary>
    public IReadOnlyList<(string partyId, int remaining)> GetRemainingByParty()
    {
        lock (gate)
        {
            var result = new List<(string, int)>(parties.Count);
            foreach (var party in parties)
                result.Add((party.PartyId, CountAvailableRemaining(party)));
            return result;
        }
    }

    /// <summary>Remaining troop count for one party, or zero when it is absent or exhausted.</summary>
    public int GetRemainingForParty(string partyId)
    {
        lock (gate)
        {
            foreach (var party in parties)
                if (party.PartyId == partyId)
                    return CountAvailableRemaining(party);
            return 0;
        }
    }

    /// <summary>Read one party's authoritative totals and pointers from the current snapshot.</summary>
    public bool TryGetPartyCounts(string partyId, out int total, out int supplied, out int initial)
    {
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId) continue;

                total = party.Entries.Length;
                supplied = party.Supplied;
                initial = party.InitialSpawnCount;
                return true;
            }
        }

        total = 0;
        supplied = 0;
        initial = 0;
        return false;
    }

    /// <summary>Whether this authoritative reserve snapshot still contains a party.</summary>
    public bool ContainsParty(string partyId)
    {
        lock (gate)
        {
            foreach (var party in parties)
                if (party.PartyId == partyId)
                    return true;
            return false;
        }
    }

    /// <summary>Whether the server has recorded this troop as permanently gone from the mission.</summary>
    public bool WasDeparted(int troopSeed)
    {
        lock (gate)
        {
            foreach (var party in parties)
                if (party.DepartedSeeds.Contains(troopSeed))
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Claim the missing live troops of a newly-owned migration party for explicit recovery. The native pointer
    /// is parked at the end while the claim is active so the wave path cannot consume the same entries, while
    /// reportable progress advances only as the recovery queue actually fields them.
    /// </summary>
    public IReadOnlyList<CoopAgentOrigin> ClaimRecoveryTroops(
        string partyId,
        IReadOnlyDictionary<string, int> neededByCharacter,
        ISet<int> recoverableSeeds)
    {
        var origins = new List<CoopAgentOrigin>();
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId) continue;
                if (party.PendingClaimSeeds != null) break;

                var remainingNeeded = new Dictionary<string, int>();
                foreach (var pair in neededByCharacter)
                    remainingNeeded[pair.Key] = pair.Value;
                foreach (var entry in party.Entries)
                {
                    if (!recoverableSeeds.Contains(entry.Seed)) continue;
                    if (!remainingNeeded.TryGetValue(entry.CharacterId, out var needed) || needed <= 0) continue;

                    if (CreateOrigin(entry, partyId) is CoopAgentOrigin origin)
                    {
                        origins.Add(origin);
                        remainingNeeded[entry.CharacterId] = needed - 1;
                    }
                }

                if (origins.Count > 0)
                {
                    party.ClaimBaseSupplied = party.Supplied;
                    party.PendingClaimSeeds = new HashSet<int>();
                    foreach (var origin in origins)
                        party.PendingClaimSeeds.Add(origin.UniqueSeed);
                    party.ParkClaimsAtEnd = true;
                    party.Supplied = party.Entries.Length;
                }
                break;
            }
        }
        return origins;
    }

    /// <summary>
    /// Consume one party entry for an explicit spawn attempt without reporting it supplied until
    /// <see cref="TryUseClaimedTroop"/> confirms that the agent exists in the mission. A null origin is an
    /// unresolvable authoritative entry and is consumed normally because there is no seed to commit.
    /// </summary>
    public bool TryClaimOneTroopFromParty(string partyId, out IAgentOriginBase origin)
    {
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId) continue;
                if (party.PendingClaimSeeds != null)
                {
                    origin = null;
                    return false;
                }
                if (party.Supplied >= party.Entries.Length)
                {
                    origin = null;
                    return false;
                }

                int claimIndex = party.Supplied;
                origin = CreateOrigin(party.Entries[claimIndex], party.PartyId);
                party.Supplied++;

                if (origin is CoopAgentOrigin claimedOrigin)
                {
                    if (party.PendingClaimSeeds == null)
                    {
                        party.ClaimBaseSupplied = claimIndex;
                        party.PendingClaimSeeds = new HashSet<int>();
                        party.ParkClaimsAtEnd = false;
                    }
                    party.PendingClaimSeeds.Add(claimedOrigin.UniqueSeed);
                }
                return true;
            }

            origin = null;
            return false;
        }
    }

    /// <summary>
    /// Run one claimed troop's use while reserve replacement is excluded, then make it reportable only when
    /// the callback confirms the agent exists. A failed callback leaves the exact claim pending for retry.
    /// </summary>
    public ClaimedTroopUseResult TryUseClaimedTroop(
        string partyId,
        int troopSeed,
        Func<bool> tryUse)
    {
        if (tryUse == null) throw new ArgumentNullException(nameof(tryUse));

        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId || party.PendingClaimSeeds == null) continue;
                if (!party.PendingClaimSeeds.Contains(troopSeed))
                    return ClaimedTroopUseResult.ClaimMissing;
                if (!tryUse())
                    return ClaimedTroopUseResult.Deferred;

                party.PendingClaimSeeds.Remove(troopSeed);
                if (party.PendingClaimSeeds.Count == 0)
                {
                    party.PendingClaimSeeds = null;
                    party.ParkClaimsAtEnd = false;
                }
                return ClaimedTroopUseResult.Committed;
            }
        }

        return ClaimedTroopUseResult.ClaimMissing;
    }

    private static int GetReportableSupplied(PartyState party)
    {
        if (party.PendingClaimSeeds == null)
            return party.Supplied;

        for (int index = Math.Max(0, party.ClaimBaseSupplied); index < party.Entries.Length; index++)
            if (party.PendingClaimSeeds.Contains(party.Entries[index].Seed))
                return index;

        return party.Supplied;
    }

    private static void PreservePendingClaims(PartyState prior, PartyState current, int serverSupplied)
    {
        if (prior?.PendingClaimSeeds == null) return;

        var currentSeeds = new HashSet<int>();
        foreach (var entry in current.Entries)
            currentSeeds.Add(entry.Seed);

        var pending = new HashSet<int>();
        foreach (var seed in prior.PendingClaimSeeds)
            if (currentSeeds.Contains(seed))
                pending.Add(seed);
        if (pending.Count == 0) return;

        current.ClaimBaseSupplied = Math.Min(
            current.Entries.Length,
            Math.Max(prior.ClaimBaseSupplied, Math.Max(0, serverSupplied)));
        current.PendingClaimSeeds = pending;
        current.ParkClaimsAtEnd = prior.ParkClaimsAtEnd;
        if (current.ParkClaimsAtEnd)
            current.Supplied = current.Entries.Length;
    }

    /// <summary>Total troops this side's supplier owns (across its parties), regardless of supplied state —
    /// the per-side count the coop spawn handler sizes the engine's deployment to.</summary>
    public int TotalTroops
    {
        get
        {
            lock (gate)
            {
                int total = 0;
                foreach (var party in parties)
                    total += party.Entries.Length;
                return total;
            }
        }
    }

    /// <summary>The persistent initial-spawn entitlement across this side's owned parties. Full reserves remain
    /// available for later reinforcement waves through <see cref="TotalTroops"/>.</summary>
    public int InitialTroops
    {
        get
        {
            lock (gate)
            {
                int total = 0;
                foreach (var party in parties)
                    total += party.InitialSpawnCount;
                return total;
            }
        }
    }

    public int NumTroopsNotSupplied
    {
        get
        {
            lock (gate)
            {
                int notSupplied = 0;
                foreach (var party in parties)
                {
                    int supplied = party.PendingClaimSeeds != null && !party.ParkClaimsAtEnd
                        ? GetReportableSupplied(party)
                        : party.Supplied;
                    notSupplied += CountAvailableRemaining(party, supplied);
                }
                return notSupplied;
            }
        }
    }

    // True while the reserve hasn't arrived (so deployment waits rather than concluding "no troops") and
    // while any party still has troops to supply.
    public bool AnyTroopRemainsToBeSupplied
    {
        get
        {
            lock (gate)
            {
                if (!populated) return true;
                foreach (var party in parties)
                {
                    int supplied = party.PendingClaimSeeds != null && !party.ParkClaimsAtEnd
                        ? GetReportableSupplied(party)
                        : party.Supplied;
                    if (CountAvailableRemaining(party, supplied) > 0) return true;
                }
                return false;
            }
        }
    }

    public IEnumerable<IAgentOriginBase> SupplyTroops(int numberToAllocate)
    {
        var origins = new List<IAgentOriginBase>();
        lock (gate)
        {
            int remaining = Math.Max(0, numberToAllocate);
            bool initialPull = initialSupplyActive;
            while (remaining > 0)
            {
                bool supplied = initialPull
                    ? TrySupplyInitialTroop(out var origin)
                    : TrySupplyNextTroop(out origin);
                if (!supplied) break;

                remaining--;
                if (origin != null) origins.Add(origin);
            }

            if (initialPull && initialSupplyRemaining.Count == 0)
                initialSupplyActive = false;
        }
        Logger.Information("[TroopSupply] {MapEvent} side {Side}: SupplyTroops({Req}) -> {Ret} origins, {Remaining} remaining",
            MapEventId, Side, numberToAllocate, origins.Count, NumTroopsNotSupplied);
        return origins;
    }

    public IAgentOriginBase SupplyOneTroop()
    {
        lock (gate)
        {
            bool supplied = TrySupplyNextAvailableTroop(out var origin);
            if (initialSupplyActive && initialSupplyRemaining.Count == 0)
                initialSupplyActive = false;
            return supplied ? origin : null;
        }
    }

    private bool TrySupplyNextAvailableTroop(out IAgentOriginBase origin)
    {
        return initialSupplyActive
            ? TrySupplyInitialTroop(out origin)
            : TrySupplyNextTroop(out origin);
    }

    private bool TrySupplyInitialTroop(out IAgentOriginBase origin)
    {
        foreach (var party in parties)
        {
            if (!initialSupplyRemaining.TryGetValue(party.PartyId, out var remaining)) continue;
            if (party.PendingClaimSeeds != null) continue;
            SkipDepartedEntries(party, party.InitialSpawnCount);
            if (remaining <= 0
                || party.Supplied >= party.Entries.Length
                || party.Supplied >= party.InitialSpawnCount)
            {
                initialSupplyRemaining.Remove(party.PartyId);
                continue;
            }

            origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
            party.Supplied++;
            if (remaining == 1)
                initialSupplyRemaining.Remove(party.PartyId);
            else
                initialSupplyRemaining[party.PartyId] = remaining - 1;
            return true;
        }

        var unavailable = new List<string>();
        foreach (var partyId in initialSupplyRemaining.Keys)
            if (GetRemainingForPartyInternal(partyId) == 0)
                unavailable.Add(partyId);
        foreach (var partyId in unavailable)
            initialSupplyRemaining.Remove(partyId);

        origin = null;
        return false;
    }

    private bool TrySupplyNextTroop(out IAgentOriginBase origin)
    {
        foreach (var party in parties)
        {
            if (party.PendingClaimSeeds != null) continue;
            SkipDepartedEntries(party);
            if (party.Supplied >= party.Entries.Length) continue;

            origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
            party.Supplied++;
            return true;
        }

        origin = null;
        return false;
    }

    private int GetRemainingForPartyInternal(string partyId)
    {
        foreach (var party in parties)
            if (party.PartyId == partyId)
                return CountAvailableRemaining(party);
        return 0;
    }

    private int GetRemainingInitialForPartyInternal(string partyId)
    {
        foreach (var party in parties)
            if (party.PartyId == partyId)
                return CountAvailableInitialRemaining(party);
        return 0;
    }

    private static int CountAvailableRemaining(PartyState party, int? supplied = null)
    {
        int count = 0;
        int start = Math.Min(Math.Max(0, supplied ?? party.Supplied), party.Entries.Length);
        for (int i = start; i < party.Entries.Length; i++)
            if (!party.DepartedSeeds.Contains(party.Entries[i].Seed))
                count++;
        return count;
    }

    private static int CountAvailableInitialRemaining(PartyState party)
    {
        int count = 0;
        int end = Math.Min(party.InitialSpawnCount, party.Entries.Length);
        for (int i = Math.Min(Math.Max(0, party.Supplied), end); i < end; i++)
            if (!party.DepartedSeeds.Contains(party.Entries[i].Seed))
                count++;
        return count;
    }

    private static void SkipDepartedEntries(PartyState party, int? end = null)
    {
        int limit = Math.Min(end ?? party.Entries.Length, party.Entries.Length);
        while (party.Supplied < limit
            && party.DepartedSeeds.Contains(party.Entries[party.Supplied].Seed))
            party.Supplied++;
    }

    /// <summary>Supply the next remaining troop from one party without consuming any other party.</summary>
    public IAgentOriginBase SupplyOneTroopFromParty(string partyId)
    {
        TrySupplyOneTroopFromParty(partyId, out var origin);
        return origin;
    }

    /// <summary>Consume the next reserve entry from one party. Returns false only when no entry remained;
    /// a true result can carry a null origin when the authoritative entry no longer resolves locally.</summary>
    public bool TrySupplyOneTroopFromParty(string partyId, out IAgentOriginBase origin)
    {
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId) continue;
                if (party.PendingClaimSeeds != null)
                {
                    origin = null;
                    return false;
                }
                SkipDepartedEntries(party);
                if (party.Supplied >= party.Entries.Length)
                {
                    origin = null;
                    return false;
                }

                origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
                party.Supplied++;
                return true;
            }

            origin = null;
            return false;
        }
    }

    public IEnumerable<IAgentOriginBase> GetAllTroops()
    {
        var origins = new List<IAgentOriginBase>();
        lock (gate)
        {
            foreach (var party in parties)
                foreach (var entry in party.Entries)
                {
                    if (party.DepartedSeeds.Contains(entry.Seed)) continue;
                    var origin = CreateOrigin(entry, party.PartyId);
                    if (origin != null) origins.Add(origin);
                }
        }
        return origins;
    }

    public BasicCharacterObject GetGeneralCharacter()
    {
        lock (gate)
        {
            foreach (var party in parties)
                foreach (var entry in party.Entries)
                    if (TryResolveCharacter(entry, out var character) && character.IsHero)
                        return character;
        }
        return null;
    }

    // The local player commands the troops it owns, so the whole owned reserve is player-controllable.
    public int GetNumberOfPlayerControllableTroops()
    {
        lock (gate)
        {
            int count = 0;
            foreach (var party in parties)
                count += CountAvailableRemaining(party, supplied: 0);
            return count;
        }
    }

    public PartyBase GetParty(UniqueTroopDescriptor troopDescriptor)
    {
        string partyId;
        lock (gate)
            seedToPartyId.TryGetValue(troopDescriptor.UniqueSeed, out partyId);

        return ResolveParty(partyId);
    }

    // partyId is a MapEventParty object id (what the builder stored), not a MobileParty id. MapEventParty.Party
    // is the PartyBase the engine needs for the agent's team/combatant and player-command checks.
    private PartyBase ResolveParty(string partyId)
    {
        if (partyId != null
            && objectManager != null
            && objectManager.TryGetObject<MapEventParty>(partyId, out var mapEventParty))
            return mapEventParty?.Party;

        return null;
    }

    // [BR-073] Origin→supplier casualty feedback, called by this supplier's own CoopAgentOrigins (one-shot
    // per origin) when the removal prefix reports a wound/kill/rout. NumRemovedTroops is the engine's only
    // casualty input for reinforcements (NumberOfActiveTroops = spawned − removed), so without these the
    // wave gate never opens. ENGINE BOOKKEEPING ONLY — roster casualties remain single-sourced on the
    // network death path (MapEventParty.OnTroop*). Seed-scoped so a descriptor this supplier doesn't own
    // (a foreign or puppet seed) can never perturb this side's count — a side that locally spawned 0 must
    // never go negative and corrupt IsSideDepleted / the wave math. Locked: supply runs on the game thread
    // while replicated removals can arrive off it.
    public void OnTroopWounded(UniqueTroopDescriptor troopDescriptor)
    {
        lock (gate) { if (seedToPartyId.ContainsKey(troopDescriptor.UniqueSeed)) numWounded++; }
    }

    public void OnTroopKilled(UniqueTroopDescriptor troopDescriptor)
    {
        lock (gate) { if (seedToPartyId.ContainsKey(troopDescriptor.UniqueSeed)) numKilled++; }
    }

    public void OnTroopRouted(UniqueTroopDescriptor troopDescriptor, bool isOrderRetreat)
    {
        lock (gate) { if (seedToPartyId.ContainsKey(troopDescriptor.UniqueSeed)) numRouted++; }
    }

    public void OnTroopScoreHit(UniqueTroopDescriptor descriptor, BasicCharacterObject attackedCharacter, int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon) { }

    private IAgentOriginBase CreateOrigin(TroopReserveEntry entry, string partyId)
    {
        if (!TryResolveCharacter(entry, out var character))
        {
            Logger.Warning("[TroopSupply] {Side} could not resolve character {CharId} (seed={Seed}) — not spawning",
                Side, entry.CharacterId, entry.Seed);
            return null;
        }
        // CoopAgentOrigin carries the troop's party for ALL troops (SimpleAgentOrigin gives non-heroes a null
        // party → no team → no spawn) and the server's descriptor, so every client agrees on troop identity.
        // It also carries this supplier, so removals feed back into NumRemovedTroops (the engine's
        // reinforcement quota) — see OnTroopWounded/Killed/Routed above.
        var party = ResolveParty(partyId);
        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(entry.Seed), partyId, this);
        if (party == null)
            Logger.Warning("[TroopSupply] {Side} origin char={Char} (isHero={Hero}) got NULL party — partyId {PartyId} unresolvable → no team / not player-commanded",
                Side, entry.CharacterId, character.IsHero, partyId);
        else if (character.IsHero)
            Logger.Information("[TroopSupply] {Side} HERO origin char={Char} party={Party} isMainParty={Main} underPlayersCmd={Cmd}",
                Side, entry.CharacterId, party.Name, party == PartyBase.MainParty, origin.IsUnderPlayersCommand);
        return origin;
    }

    // Heroes and regular troops alike are keyed by their CharacterObject id (hero CharacterObjects are
    // registered — CharacterObjectRegistry), so resolve uniformly; hero-ness is read from character.IsHero.
    private bool TryResolveCharacter(TroopReserveEntry entry, out CharacterObject character)
    {
        character = null;
        return objectManager != null && objectManager.TryGetObject<CharacterObject>(entry.CharacterId, out character);
    }
}
