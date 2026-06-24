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
    private bool populated;
    private int numWounded, numKilled, numRouted;

    public string MapEventId { get; }
    public BattleSideEnum Side { get; }

    public CoopTroopSupplier(string mapEventId, BattleSideEnum side)
    {
        MapEventId = mapEventId;
        Side = side;
    }

    /// <summary>
    /// [Network thread] Replace this side's reserve with the server's authoritative set (each party with its
    /// current supplied pointer — 0 at battle start, the server's pointer on migration). Marks us populated,
    /// so a side this client owns nothing on (empty set) reports "done" instead of blocking deployment.
    /// </summary>
    public void SetReserve(IReadOnlyList<PartyReserve> reserve)
    {
        lock (gate)
        {
            parties.Clear();
            if (reserve != null)
            {
                foreach (var party in reserve)
                {
                    var entries = party.Entries ?? Array.Empty<TroopReserveEntry>();
                    parties.Add(new PartyState
                    {
                        PartyId = party.PartyId,
                        Entries = entries,
                        Supplied = Math.Min(Math.Max(0, party.SuppliedCount), entries.Length),
                    });
                }
            }
            populated = true;
        }

        Logger.Information("[TroopSupply] Supplier {MapEvent} side {Side}: SetReserve {Parties} parties / {Entries} troops",
            MapEventId, Side, parties.Count, NumTroopsNotSupplied);
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

    public int NumRemovedTroops => numWounded + numKilled + numRouted;

    /// <summary>Whether the server's reserve has arrived (counts/identity known and final).</summary>
    public bool IsPopulated { get { lock (gate) { return populated; } } }

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
                    if (entry.IsHero && TryResolveCharacter(entry, out var character))
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
        return ResolveParty(FindPartyId(troopDescriptor.UniqueSeed));
    }

    // partyId is a MapEventParty object id (what the builder stored), not a MobileParty id. MapEventParty.Party
    // is the PartyBase the engine needs for the agent's team/combatant and player-command checks.
    private static PartyBase ResolveParty(string partyId)
    {
        if (partyId != null
            && ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            && objectManager.TryGetObject<MapEventParty>(partyId, out var mapEventParty))
            return mapEventParty?.Party;

        return null;
    }

    public void OnTroopWounded(UniqueTroopDescriptor troopDescriptor) { numWounded++; }
    public void OnTroopKilled(UniqueTroopDescriptor troopDescriptor) { numKilled++; }
    public void OnTroopRouted(UniqueTroopDescriptor troopDescriptor, bool isOrderRetreat) { numRouted++; }
    public void OnTroopScoreHit(UniqueTroopDescriptor descriptor, BasicCharacterObject attackedCharacter, int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon) { }

    private string FindPartyId(int seed)
    {
        lock (gate)
        {
            foreach (var party in parties)
                foreach (var entry in party.Entries)
                    if (entry.Seed == seed)
                        return party.PartyId;
        }
        return null;
    }

    private IAgentOriginBase CreateOrigin(TroopReserveEntry entry, string partyId)
    {
        if (!TryResolveCharacter(entry, out var character))
        {
            Logger.Warning("[TroopSupply] {Side} could not resolve character {CharId} (isHero={Hero}, seed={Seed}) — not spawning",
                Side, entry.CharacterId, entry.IsHero, entry.Seed);
            return null;
        }
        // CoopAgentOrigin carries the troop's party for ALL troops (SimpleAgentOrigin gives non-heroes a null
        // party → no team → no spawn) and the server's descriptor, so every client agrees on troop identity.
        var party = ResolveParty(partyId);
        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(entry.Seed));
        if (party == null)
            Logger.Warning("[TroopSupply] {Side} origin char={Char} (isHero={Hero}) got NULL party — partyId {PartyId} unresolvable → no team / not player-commanded",
                Side, entry.CharacterId, entry.IsHero, partyId);
        else if (entry.IsHero)
            Logger.Information("[TroopSupply] {Side} HERO origin char={Char} party={Party} isMainParty={Main} underPlayersCmd={Cmd}",
                Side, entry.CharacterId, party.Name, party == PartyBase.MainParty, origin.IsUnderPlayersCommand);
        return origin;
    }

    private static bool TryResolveCharacter(TroopReserveEntry entry, out CharacterObject character)
    {
        character = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return false;

        if (entry.IsHero)
        {
            if (objectManager.TryGetObject<Hero>(entry.CharacterId, out var hero))
                character = hero?.CharacterObject;
            return character != null;
        }

        return objectManager.TryGetObject<CharacterObject>(entry.CharacterId, out character);
    }
}
