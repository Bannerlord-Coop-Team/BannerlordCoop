using Common.Logging;
using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
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
    IReadOnlyList<SideReserve> GetOwnedReserves(MapEvent mapEvent, string controllerId, bool isHost);

    /// <summary>Forget a controller's parties because it RETREATED: drop them from the ledger and the built-set
    /// so that, if it rejoins, its party is re-flattened fresh (supplied pointer reset) and re-spawns. Do NOT
    /// call this on a disconnect — there the host adopts the troops, and resetting would double-spawn them.</summary>
    void ForgetController(MapEvent mapEvent, string controllerId);
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

    public IReadOnlyList<SideReserve> GetOwnedReserves(MapEvent mapEvent, string controllerId, bool isHost)
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

            // A party is owned by the player whose party it is, else by the host. The requester owns it when
            // it is that player, or — for an AI party — when the requester is the host.
            bool ownedByRequester = TryGetOwningPlayer(party, out var ownerControllerId)
                ? ownerControllerId == controllerId
                : isHost;
            if (!ownedByRequester)
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
            var character = element.Troop;
            if (character == null)
                continue;

            string characterId;
            if (character.IsHero)
            {
                if (character.HeroObject == null || !objectManager.TryGetId(character.HeroObject, out characterId))
                {
                    Logger.Warning("[TroopSupply] Skipped hero {Char} (HeroObject unresolvable on server) — player won't attach",
                        character.StringId);
                    continue;
                }
            }
            else if (!objectManager.TryGetId(character, out characterId))
            {
                continue;
            }

            entries.Add(new TroopReserveEntry(
                element.Descriptor.UniqueSeed, characterId, character.IsHero, (int)character.GetFormationClass()));
        }
        return entries;
    }

    private bool TryGetOwningPlayer(MapEventParty party, out string controllerId)
    {
        controllerId = null;

        var mobileParty = party.Party?.MobileParty;
        if (mobileParty == null || !objectManager.TryGetId(mobileParty, out var mobilePartyId))
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
