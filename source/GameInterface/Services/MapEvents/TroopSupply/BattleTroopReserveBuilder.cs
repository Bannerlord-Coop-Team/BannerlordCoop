using Common.Logging;
using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>The reserves a controller owns on one battle side (one entry per owned party; empty if none).</summary>
public readonly struct SideReserve
{
    public readonly BattleSideEnum Side;
    public readonly PartyReserve[] Parties;

    /// <summary>
    /// Every troop on this side across ALL owners, not just <see cref="Parties"/>. The spawn logic splits a
    /// fixed battle size in proportion to the totals each client gives it, so sizing from owned troops alone
    /// makes a side that is divided between players measure smaller than it is.
    /// </summary>
    public readonly int TotalTroops;

    /// <summary>
    /// How many parties on this side belong to a player, counting every owner and not just this receiver.
    /// One troop of the allocation is reserved for each, so that no player can be rounded down to nothing
    /// while the total still adds up exactly. See <see cref="PartyReserve.PlayerOwnedRank"/>.
    /// </summary>
    public readonly int PlayerOwnedPartyCount;

    public SideReserve(BattleSideEnum side, PartyReserve[] parties, int totalTroops = 0,
        int playerOwnedPartyCount = 0)
    {
        Side = side;
        Parties = parties;
        TotalTroops = totalTroops;
        PlayerOwnedPartyCount = playerOwnedPartyCount;
    }
}

/// <summary>
/// [Server] Builds the authoritative <see cref="IBattleTroopLedger"/> for a battle by flattening every
/// party's roster once (the server owns the resulting descriptor seeds), and resolves which reserves a given
/// controller owns: its own party always, plus — when it is the host — every AI/enemy party that no present
/// player owns. The host handler sends those reserves to the entering client to feed its troop supplier.
/// </summary>
public interface IBattleTroopReserveBuilder : IGameAbstraction
{
    /// <summary>
    /// The reserves <paramref name="controllerId"/> currently owns. A party whose resolved owning controller
    /// is in <paramref name="absentControllers"/> (a member explicitly marked absent without withdrawing) is
    /// treated as unowned, so it falls to the host. <paramref name="presentControllers"/> limits player
    /// ownership to connected or entered battle participants, preventing a stale registration from claiming
    /// a reserve.
    /// </summary>
    IReadOnlyList<SideReserve> GetOwnedReserves(MapEvent mapEvent, string controllerId, bool isHost,
        IReadOnlyCollection<string> absentControllers = null,
        IReadOnlyCollection<string> presentControllers = null);

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
        IReadOnlyCollection<string> absentControllers = null,
        IReadOnlyCollection<string> presentControllers = null)
    {
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
            return Array.Empty<SideReserve>();

        EnsureBuilt(mapEvent, mapEventId);

        var attacker = new List<PartyReserve>();
        var defender = new List<PartyReserve>();
        int attackerTotal = 0;
        int defenderTotal = 0;
        int attackerPlayerParties = 0;
        int defenderPlayerParties = 0;

        foreach (var party in EnumerateParties(mapEvent))
        {
            if (!objectManager.TryGetId(party, out var partyId))
                continue;

            // Who fields this party: its own player; or — for an AI party in a player-led army — that army
            // leader (#3 "army leader deploys the army"); or, when no player does (including a player that
            // DROPPED from this battle and hasn't returned), the host.
            if (!ledger.TryGetReserve(mapEventId, partyId, out var entries, out var supplied))
                continue;

            // Counted before ownership is considered: the totals describe the SIDE, and every client must
            // receive the same pair or their battle-size splits disagree.
            //
            // The running total before this party is added is also its OFFSET within the side. Because this
            // loop runs over every party in a fixed order on the server, those offsets partition the side
            // exactly once, which is what lets each owner take a slice that adds up (see PartyReserve).
            var partySide = party.Party?.Side ?? BattleSideEnum.None;
            var partyOffset = partySide == BattleSideEnum.Attacker ? attackerTotal : defenderTotal;
            if (partySide == BattleSideEnum.Attacker) attackerTotal += entries.Count;
            else defenderTotal += entries.Count;

            TryGetOwningPlayer(party, absentControllers, presentControllers, out var partyOwnerController);
            TryGetArmyLeaderPlayer(party, absentControllers, presentControllers, out var armyLeaderController);
            var owningController = ResolveOwningController(partyOwnerController, armyLeaderController, absentControllers);

            // Only a present player's own party reserves a player slot. Offline or absent registrations fall
            // to the host and must not reduce the allocation available to the players who entered this battle.
            var playerOwnedRank = -1;
            if (entries.Count > 0
                && ResolveOwningController(partyOwnerController, null, absentControllers) != null)
            {
                playerOwnedRank = partySide == BattleSideEnum.Attacker
                    ? attackerPlayerParties++
                    : defenderPlayerParties++;
            }

            if (!IsOwnedByRequester(owningController, controllerId, isHost))
                continue;

            var entriesArray = new TroopReserveEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++) entriesArray[i] = entries[i];

            var reserve = new PartyReserve(
                partyId,
                supplied,
                entriesArray,
                isReceiverPlayerParty: IsPartyRegisteredToController(party, controllerId),
                sideOffset: partyOffset,
                playerOwnedRank: playerOwnedRank);
            if (partySide == BattleSideEnum.Attacker)
                attacker.Add(reserve);
            else
                defender.Add(reserve);
        }

        Logger.Information("[TroopSupply] Owned reserves for {Controller} (isHost={IsHost}): Attacker={AtkParties}p/{AtkEntries}e, Defender={DefParties}p/{DefEntries}e",
            controllerId, isHost, attacker.Count, CountEntries(attacker), defender.Count, CountEntries(defender));

        // Return both sides (empty parties = "owns nothing here") so every supplier becomes populated.
        return new[]
        {
            new SideReserve(BattleSideEnum.Attacker, attacker.ToArray(), attackerTotal, attackerPlayerParties),
            new SideReserve(BattleSideEnum.Defender, defender.ToArray(), defenderTotal, defenderPlayerParties),
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
                if (!IsPartyRegisteredToController(party, controllerId)) continue;

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

                if (builtParties.Contains(partyId))
                    continue; // already flattened

                bool hadRoster = party._roster != null;
                var entries = FlattenParty(party);
                MovePlayerHeroFirst(party, entries);
                ledger.SetReserve(mapEventId, partyId, entries);
                builtParties.Add(partyId);
                Logger.Information("[TroopSupply] Built reserve: party {PartyId} side {Side} -> {Count} troops (roster was {Roster})",
                    partyId, party.Party?.Side, entries.Count, hadRoster ? "present" : "null");
            }
        }
    }

    // Hand out the server's current flattened descriptors so every client spawns the same agent identities.
    // Setup may re-flatten the server roster later, so authoritative applies match by CharacterId instead.
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

    private void MovePlayerHeroFirst(MapEventParty party, List<TroopReserveEntry> entries)
    {
        var mobileParty = party.Party?.MobileParty;
        if (mobileParty == null || !objectManager.TryGetId(mobileParty, out var mobilePartyId)) return;

        string characterId = null;
        foreach (var player in playerManager.Players)
        {
            if (player.MobilePartyId != mobilePartyId) continue;
            characterId = player.CharacterObjectId;
            break;
        }
        if (string.IsNullOrEmpty(characterId)) return;

        int heroIndex = entries.FindIndex(entry => entry.CharacterId == characterId);
        if (heroIndex <= 0) return;

        var hero = entries[heroIndex];
        entries.RemoveAt(heroIndex);
        entries.Insert(0, hero);
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

    private bool TryGetOwningPlayer(MapEventParty party, IReadOnlyCollection<string> absentControllers,
        IReadOnlyCollection<string> presentControllers, out string controllerId)
    {
        controllerId = null;
        var mobileParty = party.Party?.MobileParty;
        return mobileParty != null && TryGetPlayerController(mobileParty, absentControllers,
            presentControllers, out controllerId);
    }

    // The controller of the player who LEADS this party's army, if the army's leader party is a player's. Null
    // when the party is not in an army, or the army is led by an AI lord.
    private bool TryGetArmyLeaderPlayer(MapEventParty party, IReadOnlyCollection<string> absentControllers,
        IReadOnlyCollection<string> presentControllers, out string controllerId)
    {
        controllerId = null;
        var leaderMobileParty = party.Party?.MobileParty?.Army?.LeaderParty;
        return leaderMobileParty != null && TryGetPlayerController(leaderMobileParty, absentControllers,
            presentControllers, out controllerId);
    }

    // Present battle members win. An absent member is retained only so ResolveOwningController can hand its
    // party to the host; unrelated stale/offline registrations do not own reserves.
    private bool TryGetPlayerController(MobileParty mobileParty, IReadOnlyCollection<string> absentControllers,
        IReadOnlyCollection<string> presentControllers, out string controllerId)
    {
        controllerId = null;
        if (!objectManager.TryGetId(mobileParty, out var mobilePartyId))
            return false;

        controllerId = ResolvePlayerController(playerManager.Players, mobilePartyId,
            presentControllers, absentControllers);
        return controllerId != null;
    }

    internal static string ResolvePlayerController(IEnumerable<Player> players, string mobilePartyId,
        IReadOnlyCollection<string> presentControllers = null,
        IReadOnlyCollection<string> absentControllers = null)
    {
        string registeredController = null;
        string absentController = null;
        foreach (var player in players)
        {
            if (player.MobilePartyId != mobilePartyId) continue;
            if (presentControllers?.Contains(player.ControllerId) == true) return player.ControllerId;
            if (registeredController == null) registeredController = player.ControllerId;
            if (absentController == null && absentControllers?.Contains(player.ControllerId) == true)
                absentController = player.ControllerId;
        }

        return absentController ?? (presentControllers == null ? registeredController : null);
    }

    private bool IsPartyRegisteredToController(MapEventParty party, string controllerId)
    {
        var mobileParty = party.Party?.MobileParty;
        if (mobileParty == null || !objectManager.TryGetId(mobileParty, out var mobilePartyId))
            return false;

        return IsPartyRegisteredToController(playerManager.Players, mobilePartyId, controllerId);
    }

    internal static bool IsPartyRegisteredToController(IEnumerable<Player> players, string mobilePartyId,
        string controllerId)
    {
        return players.Any(player => player.ControllerId == controllerId && player.MobilePartyId == mobilePartyId);
    }

    private static IEnumerable<MapEventParty> EnumerateParties(MapEvent mapEvent)
    {
        foreach (var party in mapEvent.AttackerSide.Parties)
            yield return party;
        foreach (var party in mapEvent.DefenderSide.Parties)
            yield return party;
    }
}
