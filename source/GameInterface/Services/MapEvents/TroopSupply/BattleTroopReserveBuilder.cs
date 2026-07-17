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

/// <summary>
/// [Server] Builds the authoritative <see cref="IBattleTroopLedger"/> for a battle by flattening every
/// party's roster once (the server owns the resulting descriptor seeds), and resolves which reserves a given
/// controller owns: its own party always, plus — when it is the host — every AI/enemy party that no connected
/// player owns. The host handler sends those reserves to the entering client to feed its troop supplier.
/// </summary>
public interface IBattleTroopReserveBuilder : IGameAbstraction
{
    /// <summary>
    /// The reserves <paramref name="controllerId"/> currently owns. A party whose resolved owning controller
    /// is in <paramref name="absentControllers"/> (a member explicitly marked absent without withdrawing) is
    /// treated as unowned, so it falls to the host. Player registrations survive that absence, so ownership
    /// alone cannot see it — the absent set is what re-scopes the party.
    /// </summary>
    IReadOnlyList<SideReserve> GetOwnedReserves(MapEvent mapEvent, string controllerId, bool isHost,
        IReadOnlyCollection<string> absentControllers = null);

    /// <summary>Forget a controller's withdrawn parties: drop them from the ledger and the built-set so that,
    /// if it rejoins, its party is re-flattened fresh (supplied pointer reset) and re-spawns.</summary>
    void ForgetController(MapEvent mapEvent, string controllerId);

    /// <summary>Forget EVERY reserve of a battle (its whole ledger entry + flatten cache). Called when a battle
    /// ENDS — concluded (victory) or fully ABANDONED (host left with no successors) — so the server stops
    /// holding the battle's reserves and a later battle on the SAME map event re-flattens all parties fresh
    /// (otherwise the AI/enemy parties the host had been fielding keep their advanced supplied pointers and
    /// never re-spawn on a restart).</summary>
    void ForgetMapEvent(MapEvent mapEvent);
}

/// <inheritdoc cref="IBattleTroopReserveBuilder"/>
public class BattleTroopReserveBuilder : IBattleTroopReserveBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleTroopReserveBuilder>();

    private readonly IBattleTroopLedger ledger;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    // Parties already flattened into the ledger (by object-manager id). Per-PARTY, not per-map-event, so a
    // party that joins AFTER the battle started (a mid-battle joiner) gets flattened on demand the next time
    // reserves are built — otherwise it would never be in the ledger and that player would own nothing.
    private readonly HashSet<string> builtParties = new HashSet<string>();
    private readonly object gate = new object();

    public BattleTroopReserveBuilder(IBattleTroopLedger ledger, IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.ledger = ledger;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
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

            var reserve = new PartyReserve(partyId, supplied, entriesArray);
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
                Logger.Information("[TroopSupply] Forgot party {PartyId} of retreating {Controller} (re-flattens fresh on rejoin)",
                    partyId, controllerId);
            }
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
            Logger.Information("[TroopSupply] Forgot ALL reserves of battle {MapEventId} ({Count} flatten-cache entries cleared)",
                mapEventId, forgotten);
        }
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
                var entries = FlattenParty(party);
                ledger.SetReserve(mapEventId, partyId, entries);
                Logger.Information("[TroopSupply] Built reserve: party {PartyId} side {Side} -> {Count} troops (roster was {Roster})",
                    partyId, party.Party?.Side, entries.Count, hadRoster ? "present" : "null");
            }
        }
    }

    // The server's MapEventParty._roster is the flattened roster; its descriptors are the authoritative, stable
    // seeds we hand out (and what the casualty path keys on). An enemy/AI party that was never made
    // mission-ready can have a null _roster, so flatten it here (server-side Update is allowed).
    private List<TroopReserveEntry> FlattenParty(MapEventParty party)
    {
        var entries = new List<TroopReserveEntry>();

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

            entries.Add(new TroopReserveEntry(
                element.Descriptor.UniqueSeed, characterId, (int)character.GetFormationClass()));
        }
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
    {
        controllerId = null;
        var mobileParty = party.Party?.MobileParty;
        return mobileParty != null && TryGetPlayerController(mobileParty, out controllerId);
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
    {
        controllerId = null;
        if (!objectManager.TryGetId(mobileParty, out var mobilePartyId))
            return false;

        foreach (var player in playerManager.Players)
        {
            if (player.MobilePartyId == mobilePartyId)
            {
                controllerId = player.ControllerId;
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
