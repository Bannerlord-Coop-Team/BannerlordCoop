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

    private sealed class PartyState
    {
        public string PartyId;
        public TroopReserveEntry[] Entries = Array.Empty<TroopReserveEntry>();
        public int Supplied;
        /// <summary>Where this party starts within its side; see <see cref="PartyReserve.SideOffset"/>.</summary>
        public int SideOffset;
    }

    private readonly object gate = new object();
    private readonly List<PartyState> parties = new List<PartyState>();
    // seed -> partyId, rebuilt alongside `parties` in SetReserve, so GetParty/FindPartyId is O(1) instead of
    // scanning every party's entries per agent. Entry seeds are server-unique, so one seed maps to one party.
    private readonly Dictionary<int, string> seedToPartyId = new Dictionary<int, string>();
    private string playerPartyId;
    private bool populated;
    private int sideTotalTroops;
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
        int sideTotal = 0)
    {
        var dropped = new List<(string PartyId, int Supplied)>();
        lock (gate)
        {
            // 0 means the server sent no total (older peer): keep the previous value rather than forgetting it.
            if (sideTotal > 0) sideTotalTroops = sideTotal;

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
    /// Every troop on this side across ALL owners, or <see cref="TotalTroops"/> when the server sent none.
    /// The spawn handler sizes the engine from this so each client computes the same split; the supplier then
    /// contributes only its <see cref="OwnedShareOf"/> that allocation.
    /// </summary>
    public int SideTotalTroops
    {
        get
        {
            lock (gate)
            {
                if (sideTotalTroops > 0) return sideTotalTroops;
                int owned = 0;
                foreach (var party in parties) owned += party.Entries.Length;
                return owned;
            }
        }
    }

    /// <summary>
    /// This client's slice of a side-wide allocation, in proportion to the troops it owns. Every owner runs
    /// the same sum, so the slices add up to the allocation instead of each owner serving all of it.
    /// </summary>
    public int OwnedShareOf(int sideAllocation)
    {
        if (sideAllocation <= 0) return 0;

        lock (gate)
        {
            int owned = 0;
            foreach (var party in parties) owned += party.Entries.Length;
            if (owned <= 0) return 0;

            var total = sideTotalTroops > 0 ? sideTotalTroops : owned;
            if (owned >= total) return sideAllocation;

            // Exact apportionment by cumulative flooring: each party takes the difference between the
            // allocation scaled to the END of its range and to its START. Because every party on the side
            // occupies one contiguous, non-overlapping range of [0, total), the slices taken by ALL owners
            // sum to exactly sideAllocation - no owner needs to know what the others hold.
            //
            // This replaces proportional rounding, which could overshoot the allocation, and a floor that
            // forced every owner with troops to at least one - that floor turned a one-troop wave into one
            // troop PER OWNER.
            var share = 0;
            foreach (var party in parties)
            {
                var count = party.Entries.Length;
                if (count <= 0) continue;

                var start = ScaleToAllocation(party.SideOffset, total, sideAllocation);
                var end = ScaleToAllocation(party.SideOffset + count, total, sideAllocation);
                share += end - start;
            }

            // Exact up to here, and deliberately not exact past it. A share of zero on the side holding this
            // client's own player party means that player fields nothing, which is the deployment wedge this
            // sizing exists to prevent: CheckDeployment skips a side whose reservation falls short of
            // InitialSpawnNumber, and it skips that side's plan-making too, so NOBODY on the side spawns.
            //
            // The overshoot is bounded by the number of human owners whose interval floors to zero: each adds
            // one, so the side fields at most N-1 more than the allocation for N human-owned parties. It is
            // NOT bounded by "allocation smaller than N", which an earlier version of this comment claimed -
            // uneven ownership breaks that. An owner holding 1 troop of a 1000-strong side scales to zero for
            // an allocation of 100, tops itself up to one, and the other owners still supply all 100.
            //
            // Exactness and "every player gets an agent" cannot both hold locally: the owner that would have
            // to give up a troop is a different client, and nothing here knows the others' shares. Closing it
            // would need the server to apportion centrally and send each owner its number - a wire field and a
            // migration path - to save at most one troop per player-owned party in a wave.
            //
            // Restricted to a party that still HAS troops, so an exhausted player party stops topping up
            // rather than conjuring one troop per wave for the rest of the battle.
            if (share <= 0 && ReceiverPlayerPartyHasTroops()) share = 1;

            return Math.Min(share, sideAllocation);
        }
    }

    /// <summary>Where a position within the side falls once the side is scaled to the allocation.</summary>
    /// <remarks>
    /// long arithmetic because position * allocation overflows int for a large side and a large wave, and
    /// an overflow here would silently hand out a negative or wrapped share.
    /// </remarks>
    private static int ScaleToAllocation(int position, int total, int allocation)
        => (int)((long)position * allocation / total);

    /// <summary>
    /// Whether this client owns the receiver's own party in this battle AND that party still has troops left
    /// to field. Callers already hold <see cref="gate"/>; Monitor is reentrant, so this is safe either way.
    /// </summary>
    private bool ReceiverPlayerPartyHasTroops()
    {
        if (playerPartyId == null) return false;

        lock (gate)
        {
            foreach (var party in parties)
            {
                if (party.PartyId != playerPartyId) continue;

                return party.Supplied < party.Entries.Length;
            }
        }

        return false;
    }

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
