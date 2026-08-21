using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
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

    public readonly struct AllocationSnapshot
    {
        private readonly int[] partyOffsets;
        private readonly int[] partyCounts;
        private readonly int playerOwnedPartyCount;
        private readonly bool ownsReceiverPlayerParty;
        private readonly int receiverPlayerRank;

        public long Revision { get; }
        public int BattleSize { get; }
        public int SideTotalTroops { get; }
        public int TotalTroops { get; }
        public int SuppliedTroops { get; }

        internal AllocationSnapshot(long revision, int battleSize, int sideTotalTroops, int totalTroops,
            int suppliedTroops,
            int playerOwnedPartyCount, bool ownsReceiverPlayerParty, int receiverPlayerRank,
            int[] partyOffsets, int[] partyCounts)
        {
            Revision = revision;
            BattleSize = battleSize;
            SideTotalTroops = sideTotalTroops;
            TotalTroops = totalTroops;
            SuppliedTroops = suppliedTroops;
            this.playerOwnedPartyCount = playerOwnedPartyCount;
            this.ownsReceiverPlayerParty = ownsReceiverPlayerParty;
            this.receiverPlayerRank = receiverPlayerRank;
            this.partyOffsets = partyOffsets;
            this.partyCounts = partyCounts;
        }

        public int OwnedShareOf(int sideAllocation)
        {
            if (sideAllocation <= 0 || TotalTroops <= 0) return 0;

            int total = SideTotalTroops;
            if (total <= 0) return 0;
            if (TotalTroops >= total) return sideAllocation;

            if (playerOwnedPartyCount > 0)
            {
                if (sideAllocation < playerOwnedPartyCount)
                    return receiverPlayerRank >= 0 && receiverPlayerRank < sideAllocation ? 1 : 0;

                int share = ApportionByInterval(sideAllocation - playerOwnedPartyCount, total);
                if (ownsReceiverPlayerParty) share += 1;
                return Math.Min(share, sideAllocation);
            }

            return Math.Min(ApportionByInterval(sideAllocation, total), sideAllocation);
        }

        private int ApportionByInterval(int allocation, int total)
        {
            if (allocation <= 0 || partyOffsets == null || partyCounts == null) return 0;

            int share = 0;
            for (int i = 0; i < partyCounts.Length; i++)
            {
                int count = partyCounts[i];
                if (count <= 0) continue;

                int start = ScaleToAllocation(partyOffsets[i], total, allocation);
                int end = ScaleToAllocation(partyOffsets[i] + count, total, allocation);
                share += end - start;
            }
            return share;
        }

        private static int ScaleToAllocation(int position, int total, int allocation)
            => (int)((long)position * allocation / total);
    }

    private sealed class PartyState
    {
        public string PartyId;
        public TroopReserveEntry[] Entries = Array.Empty<TroopReserveEntry>();
        public int Supplied;
        /// <summary>Where this party starts within its side; see <see cref="PartyReserve.SideOffset"/>.</summary>
        public int SideOffset;
        /// <summary>Its position among the side's player-owned parties, or -1; see <see cref="PartyReserve.PlayerOwnedRank"/>.</summary>
        public int PlayerOwnedRank;
    }

    private readonly object gate = new object();
    private readonly List<PartyState> parties = new List<PartyState>();
    // seed -> partyId, rebuilt alongside `parties` in SetReserve, so GetParty/FindPartyId is O(1) instead of
    // scanning every party's entries per agent. Entry seeds are server-unique, so one seed maps to one party.
    private readonly Dictionary<int, string> seedToPartyId = new Dictionary<int, string>();
    private string playerPartyId;
    private bool populated;
    private int sideTotalTroops;
    private int playerOwnedPartyCount;
    private long allocationRevision;
    private int battleSize;
    private int reserveRevision;
    private int numWounded, numKilled, numRouted;
    // Injected at construction (a stable per-session singleton) so the per-agent supply path resolves troop/party
    // objects without hitting the service locator each call. Null only in tests that don't exercise that path.
    private readonly IObjectManager objectManager;
    // BR-110: the engine agent budget clamps wave/initial allocation to the mission's render capacity.
    private readonly IBattleAgentBudget agentBudget;
    public string MapEventId { get; }
    public BattleSideEnum Side { get; }

    public CoopTroopSupplier(string mapEventId, BattleSideEnum side, IObjectManager objectManager,
        IBattleAgentBudget agentBudget)
    {
        MapEventId = mapEventId;
        Side = side;
        this.objectManager = objectManager;
        this.agentBudget = agentBudget;
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
    public IReadOnlyList<(string PartyId, int Supplied)> SetReserve(IReadOnlyList<PartyReserve> reserve,
        int sideTotal, int playerOwnedParties, int authoritativeBattleSize, long snapshotRevision = 0)
    {
        var dropped = new List<(string PartyId, int Supplied)>();
        lock (gate)
        {
            sideTotalTroops = Math.Max(0, sideTotal);
            playerOwnedPartyCount = Math.Max(0, playerOwnedParties);
            allocationRevision = snapshotRevision;
            battleSize = Math.Max(0, authoritativeBattleSize);

            // Capture the current per-party pointers before rebuilding. A resend can carry a STALE pointer: the
            // server's ledger lags our local supply by up to one report interval, and on migration it re-sends
            // our OWN party at that lagging value. Resuming from the server value alone would rewind a party we
            // have already supplied further and re-spawn troops already on the field (with duplicate seeds). So
            // resume from max(local, server), mirroring the server ledger's own monotonic ReportSupplied.
            var priorSupplied = new Dictionary<string, int>(parties.Count);
            foreach (var existing in parties)
                priorSupplied[existing.PartyId] = existing.Supplied;

            parties.Clear();
            seedToPartyId.Clear();
            playerPartyId = null;
            if (reserve != null)
            {
                foreach (var party in reserve)
                {
                    var entries = party.Entries ?? Array.Empty<TroopReserveEntry>();
                    int supplied = Math.Min(Math.Max(0, party.SuppliedCount), entries.Length);
                    if (priorSupplied.TryGetValue(party.PartyId, out var local) && local > supplied)
                        supplied = Math.Min(local, entries.Length);
                    priorSupplied.Remove(party.PartyId); // kept — not part of the dropped set
                    var state = new PartyState
                    {
                        PartyId = party.PartyId,
                        Entries = entries,
                        Supplied = supplied,
                        SideOffset = party.SideOffset,
                        PlayerOwnedRank = party.PlayerOwnedRank,
                    };
                    // Allocate this client's own party first. Otherwise an army's AI parties can fill the
                    // render cap before the local hero is reserved, leaving deployment without a player agent.
                    if (party.IsReceiverPlayerParty)
                        parties.Insert(0, state);
                    else
                        parties.Add(state);
                    if (party.IsReceiverPlayerParty)
                        playerPartyId = party.PartyId;
                    foreach (var entry in entries)
                        seedToPartyId[entry.Seed] = party.PartyId;
                }
            }
            populated = true;
            reserveRevision++;

            // Whatever the new set did not re-claim was DROPPED by this replace.
            foreach (var prior in priorSupplied)
                dropped.Add((prior.Key, prior.Value));
        }

        Logger.Information("[TroopSupply] Supplier {MapEvent} side {Side}: SetReserve {Parties} parties / {Entries} troops ({Dropped} parties dropped), receiver party {PlayerParty}",
            MapEventId, Side, parties.Count, NumTroopsNotSupplied, dropped.Count, PlayerPartyId);
        return dropped;
    }

    /// <summary>How many troops have been supplied per party — reported back to the server for the ledger.</summary>
    public IReadOnlyList<(string partyId, int supplied)> GetSuppliedByParty()
    {
        lock (gate)
        {
            var result = new List<(string, int)>(parties.Count);
            foreach (var party in parties)
                result.Add((party.PartyId, party.Supplied));
            return result;
        }
    }

    public int NumRemovedTroops { get { lock (gate) { return numWounded + numKilled + numRouted; } } }

    /// <summary>Whether the server's reserve has arrived (counts/identity known and final).</summary>
    public bool IsPopulated { get { lock (gate) { return populated; } } }

    /// <summary>The server-authored reserve id of this client's own party, when it belongs to this side.</summary>
    public string PlayerPartyId { get { lock (gate) { return playerPartyId; } } }

    /// <summary>Monotonic count of authoritative reserve snapshots applied to this supplier.</summary>
    public int ReserveRevision { get { lock (gate) { return reserveRevision; } } }

    /// <summary>The server-authored complete two-side snapshot generation.</summary>
    public long AllocationRevision { get { lock (gate) { return allocationRevision; } } }

    public AllocationSnapshot CaptureAllocationSnapshot()
    {
        lock (gate)
        {
            int total = 0;
            int supplied = 0;
            int receiverPlayerRank = -1;
            var partyOffsets = new int[parties.Count];
            var partyCounts = new int[parties.Count];
            for (int i = 0; i < parties.Count; i++)
            {
                var party = parties[i];
                int count = party.Entries.Length;
                partyOffsets[i] = party.SideOffset;
                partyCounts[i] = count;
                total += count;
                supplied += party.Supplied;
                if (party.PartyId != playerPartyId) continue;

                receiverPlayerRank = party.PlayerOwnedRank;
            }

            return new AllocationSnapshot(
                allocationRevision,
                battleSize,
                sideTotalTroops,
                total,
                supplied,
                playerOwnedPartyCount,
                playerPartyId != null,
                receiverPlayerRank,
                partyOffsets,
                partyCounts);
        }
    }

    /// <summary>Remaining troop count for each party in the current authoritative reserve.</summary>
    public IReadOnlyList<(string partyId, int remaining)> GetRemainingByParty()
    {
        lock (gate)
        {
            var result = new List<(string, int)>(parties.Count);
            foreach (var party in parties)
                result.Add((party.PartyId, party.Entries.Length - party.Supplied));
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
                    return party.Entries.Length - party.Supplied;
            return 0;
        }
    }

#if DEBUG
    /// <summary>Runs a debug allocation decision while reserve refreshes are blocked.</summary>
    internal TResult WithSupplyPreview<TResult>(
        int numberToAllocate,
        Func<List<IAgentOriginBase>, TResult> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var origins = new List<IAgentOriginBase>();
        lock (gate)
        {
            if (numberToAllocate > 0)
            {
                int slotBudget = agentBudget != null
                    ? agentBudget.RemainingCapacity(agentBudget.CountLiveAgents(Mission.Current))
                    : int.MaxValue;
                int allocated = 0;
                foreach (var party in parties)
                {
                    for (int index = party.Supplied;
                         allocated < numberToAllocate && index < party.Entries.Length;
                         index++)
                    {
                        var origin = CreateOrigin(party.Entries[index], party.PartyId);
                        int slots = SlotsForOrigin(origin);
                        if (slots > slotBudget) return action(origins);

                        allocated++;
                        slotBudget -= slots;
                        if (origin != null) origins.Add(origin);
                    }
                    if (allocated >= numberToAllocate) break;
                }
            }
            return action(origins);
        }
    }
#endif

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

    /// <summary>
    /// Claim the missing live troops of a newly-owned migration party for explicit recovery. The whole party
    /// is marked supplied so the native wave path cannot also spawn entries now owned by the recovery queue.
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

                party.Supplied = party.Entries.Length;
                break;
            }
        }
        return origins;
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

    /// <summary>
    /// Every troop on this side across ALL owners.
    /// The spawn handler sizes the engine from this so each client computes the same split; the supplier then
    /// contributes only its <see cref="OwnedShareOf"/> that allocation.
    /// </summary>
    public int SideTotalTroops { get { lock (gate) { return sideTotalTroops; } } }

    public int PlayerOwnedPartyCount { get { lock (gate) { return playerOwnedPartyCount; } } }

    public int BattleSize { get { lock (gate) { return battleSize; } } }

    /// <summary>
    /// This client's slice of a side-wide allocation, in proportion to the troops it owns. Every owner runs
    /// the same sum, so the slices add up to the allocation instead of each owner serving all of it.
    /// </summary>
    public int OwnedShareOf(int sideAllocation)
        => CaptureAllocationSnapshot().OwnedShareOf(sideAllocation);

    public int NumTroopsNotSupplied
    {
        get
        {
            lock (gate)
            {
                int notSupplied = 0;
                foreach (var party in parties)
                    notSupplied += party.Entries.Length - party.Supplied;
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
                    if (party.Supplied < party.Entries.Length) return true;
                return false;
            }
        }
    }

    public IEnumerable<IAgentOriginBase> SupplyTroops(int numberToAllocate)
    {
        // No apportionment here: the number arriving is ALREADY this client's share. Init is given the side
        // totals so every client computes the same battle-size split, and CoopBattleMissionSpawnHandler then
        // rewrites the phase numbers through OwnedShareOf once - so every request the engine derives from a
        // phase is this client's slice, and the other owners' agents arrive replicated as before
        // (OwnedAgentReplicator/PuppetSpawner).
        //
        // Taking the share again here would apply it twice, and the engine cannot tolerate being short-changed:
        // CheckDeployment reserves InitialSpawnNumber - ReservedTroopsCount and SKIPS THE WHOLE SIDE (its
        // plan-making included) while the count falls short. Returning a fraction of each request makes the gap
        // close geometrically and never reach zero - live, a 65-troop target stalled at 64 with the side never
        // planned, so the player's team never spawned and the player had no agent on the field.
        if (numberToAllocate <= 0) return Array.Empty<IAgentOriginBase>();

        // BR-110: allocate no more troops than the engine has RENDER-SLOT capacity for — a mounted troop needs
        // two slots (rider + horse). The unallocated remainder stays UNSUPPLIED (wave-eligible), so the native
        // wave logic re-requests it as casualties free slots; the supplied pointer stays aligned with what can
        // actually field. A null budget (the service-locator fallback path could not resolve one) means no
        // clamp, matching the no-mission behaviour. The native drip is additionally re-checked at spawn time by
        // MissionSpawnCapacityPatch, so this clamp is a pre-filter, not the sole guard.
        int slotBudget = agentBudget != null
            ? agentBudget.RemainingCapacity(agentBudget.CountLiveAgents(Mission.Current))
            : int.MaxValue;

        var origins = new List<IAgentOriginBase>();
        int supplied = 0;
        lock (gate)
        {
            bool stop = false;
            foreach (var party in parties)
            {
                while (!stop && supplied < numberToAllocate && party.Supplied < party.Entries.Length)
                {
                    var origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
                    int slots = SlotsForOrigin(origin);
                    // Stop rather than skip: the supplied pointer advances sequentially, so a troop that does
                    // not fit now must remain unsupplied (wave-eligible) instead of being jumped over.
                    if (slots > slotBudget) { stop = true; break; }

                    party.Supplied++;
                    supplied++;
                    slotBudget -= slots;
                    if (origin != null) origins.Add(origin);
                }
                if (stop || supplied >= numberToAllocate) break;
            }
        }
        Logger.Information("[TroopSupply] {MapEvent} side {Side}: SupplyTroops({Req}) -> {Ret} origins ({Withheld} withheld at the engine agent limit), {Remaining} remaining",
            MapEventId, Side, numberToAllocate, origins.Count, numberToAllocate - supplied, NumTroopsNotSupplied);
        return origins;
    }

    // BR-110: render slots one supplied origin will consume when spawned — a mounted troop spawns a rider and a
    // horse (2), an unmounted troop one (1), a null/unresolvable origin none (0, so it advances the supplied
    // pointer without charging the budget). Falls back to 1 when no budget is available (the null-budget path).
    private int SlotsForOrigin(IAgentOriginBase origin)
    {
        if (origin == null) return 0;
        return agentBudget == null ? 1 : agentBudget.SlotsForOrigin(origin);
    }

    public IAgentOriginBase SupplyOneTroop()
    {
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.Supplied < party.Entries.Length)
                {
                    var origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
                    party.Supplied++;
                    return origin;
                }
            }
            return null;
        }
    }

    /// <summary>Supply the next remaining troop from one party without consuming any other party.</summary>
    public IAgentOriginBase SupplyOneTroopFromParty(string partyId)
    {
        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != partyId) continue;
                if (party.Supplied >= party.Entries.Length) return null;

                var origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
                party.Supplied++;
                return origin;
            }
            return null;
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
                count += party.Entries.Length;
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
