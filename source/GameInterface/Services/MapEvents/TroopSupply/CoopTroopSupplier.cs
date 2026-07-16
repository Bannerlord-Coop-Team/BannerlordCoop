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
    }

    private readonly object gate = new object();
    private readonly List<PartyState> parties = new List<PartyState>();
    // seed -> partyId, rebuilt alongside `parties` in SetReserve, so GetParty/FindPartyId is O(1) instead of
    // scanning every party's entries per agent. Entry seeds are server-unique, so one seed maps to one party.
    private readonly Dictionary<int, string> seedToPartyId = new Dictionary<int, string>();
    private bool populated;
    private int reserveRevision;
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
    public IReadOnlyList<(string PartyId, int Supplied)> SetReserve(IReadOnlyList<PartyReserve> reserve)
    {
        var dropped = new List<(string PartyId, int Supplied)>();
        lock (gate)
        {
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
            if (reserve != null)
            {
                foreach (var party in reserve)
                {
                    var entries = party.Entries ?? Array.Empty<TroopReserveEntry>();
                    int supplied = Math.Min(Math.Max(0, party.SuppliedCount), entries.Length);
                    if (priorSupplied.TryGetValue(party.PartyId, out var local) && local > supplied)
                        supplied = Math.Min(local, entries.Length);
                    priorSupplied.Remove(party.PartyId); // kept — not part of the dropped set
                    parties.Add(new PartyState
                    {
                        PartyId = party.PartyId,
                        Entries = entries,
                        Supplied = supplied,
                    });
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

        Logger.Information("[TroopSupply] Supplier {MapEvent} side {Side}: SetReserve {Parties} parties / {Entries} troops ({Dropped} parties dropped)",
            MapEventId, Side, parties.Count, NumTroopsNotSupplied, dropped.Count);
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
        var origins = new List<IAgentOriginBase>();
        lock (gate)
        {
            int remaining = numberToAllocate;
            foreach (var party in parties)
            {
                while (remaining > 0 && party.Supplied < party.Entries.Length)
                {
                    var origin = CreateOrigin(party.Entries[party.Supplied], party.PartyId);
                    party.Supplied++;
                    remaining--;
                    if (origin != null) origins.Add(origin);
                }
                if (remaining == 0) break;
            }
        }
        Logger.Information("[TroopSupply] {MapEvent} side {Side}: SupplyTroops({Req}) -> {Ret} origins, {Remaining} remaining",
            MapEventId, Side, numberToAllocate, origins.Count, NumTroopsNotSupplied);
        return origins;
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
