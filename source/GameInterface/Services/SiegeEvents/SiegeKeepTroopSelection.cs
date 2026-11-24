using GameInterface.Configuration;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents;

/// <summary>One walls player's place in the keep battle.</summary>
public readonly struct SiegeKeepSeat
{
    public readonly string ControllerId;
    public readonly string MobilePartyId;
    public readonly string HeroCharacterId;
    /// <summary>How many troops besides the player's hero this player may bring.</summary>
    public readonly int EscortAllowance;

    public SiegeKeepSeat(string controllerId, string mobilePartyId, string heroCharacterId, int escortAllowance)
    {
        ControllerId = controllerId;
        MobilePartyId = mobilePartyId;
        HeroCharacterId = heroCharacterId;
        EscortAllowance = escortAllowance;
    }
}

public interface ISiegeKeepTroopSelection : IGameAbstraction
{
    /// <summary>Opens the keep stage with its seated players and the defender troops vanilla puts in the hall.</summary>
    void Open(MapEvent mapEvent, IReadOnlyList<SiegeKeepSeat> seats, IReadOnlyDictionary<string, int> defenderBudget,
        DateTime deadlineUtc);
    bool IsOpen(MapEvent mapEvent);
    bool IsLocked(MapEvent mapEvent);
    bool TryGetDeadline(MapEvent mapEvent, out DateTime deadlineUtc);
    IReadOnlyList<SiegeKeepSeat> GetSeats(MapEvent mapEvent);
    bool TryGetSeat(MapEvent mapEvent, string mobilePartyId, out SiegeKeepSeat seat,
        out HideoutTroopSelectionEntry[] selection);
    bool TrySelect(MapEvent mapEvent, MobileParty party, IReadOnlyList<HideoutTroopSelectionEntry> requested,
        out HideoutTroopSelectionEntry[] accepted);
    /// <summary>Gives up a seat that has not locked, for a player who left before the roster did.</summary>
    void RemoveSeat(MapEvent mapEvent, string mobilePartyId);
    bool HaveAllAnswered(MapEvent mapEvent);
    /// <summary>Fixes the roster and splits the defender troops between the defending parties in the map event.</summary>
    void Lock(MapEvent mapEvent);
    bool IsAdmitted(MapEvent mapEvent, MobileParty party);
    void FilterReserve(MapEvent mapEvent, MapEventParty party, List<TroopReserveEntry> entries);
    IReadOnlyList<MapEvent> GetOpenMapEvents();
    void Forget(MapEvent mapEvent);
}

/// <summary>
/// [Server] The keep (lord's hall) roster of each siege assault: which walls players have a place, the troops each
/// chose, and the defender troops vanilla's hall model picked. The reserve builder reads it to field only those.
/// </summary>
public sealed class SiegeKeepTroopSelection : ISiegeKeepTroopSelection
{
    private sealed class Attempt
    {
        public DateTime DeadlineUtc;
        public bool Locked;
        public readonly List<SiegeKeepSeat> Seats = new();
        public readonly Dictionary<string, HideoutTroopSelectionEntry[]> Selections = new();
        public readonly Dictionary<string, int> DefenderBudget = new();
        public readonly Dictionary<PartyBase, Dictionary<string, int>> DefenderAllocations = new();
    }

    private readonly IObjectManager objectManager;
    private readonly Dictionary<MapEvent, Attempt> attempts = new();
    private readonly object gate = new();

    public SiegeKeepTroopSelection(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Open(MapEvent mapEvent, IReadOnlyList<SiegeKeepSeat> seats,
        IReadOnlyDictionary<string, int> defenderBudget, DateTime deadlineUtc)
    {
        var attempt = new Attempt { DeadlineUtc = deadlineUtc };
        attempt.Seats.AddRange(seats);
        foreach (var pair in defenderBudget)
            attempt.DefenderBudget[pair.Key] = pair.Value;

        lock (gate)
            attempts[mapEvent] = attempt;
    }

    public bool IsOpen(MapEvent mapEvent)
    {
        lock (gate)
            return mapEvent != null && attempts.ContainsKey(mapEvent);
    }

    public bool IsLocked(MapEvent mapEvent)
    {
        lock (gate)
            return mapEvent != null && attempts.TryGetValue(mapEvent, out var attempt) && attempt.Locked;
    }

    public bool TryGetDeadline(MapEvent mapEvent, out DateTime deadlineUtc)
    {
        deadlineUtc = default;
        lock (gate)
        {
            if (mapEvent == null || !attempts.TryGetValue(mapEvent, out var attempt)) return false;
            deadlineUtc = attempt.DeadlineUtc;
            return true;
        }
    }

    public IReadOnlyList<SiegeKeepSeat> GetSeats(MapEvent mapEvent)
    {
        lock (gate)
            return mapEvent != null && attempts.TryGetValue(mapEvent, out var attempt)
                ? attempt.Seats.ToArray()
                : Array.Empty<SiegeKeepSeat>();
    }

    public bool TryGetSeat(MapEvent mapEvent, string mobilePartyId, out SiegeKeepSeat seat,
        out HideoutTroopSelectionEntry[] selection)
    {
        seat = default;
        selection = Array.Empty<HideoutTroopSelectionEntry>();
        lock (gate)
        {
            if (mapEvent == null || !attempts.TryGetValue(mapEvent, out var attempt)) return false;
            var index = attempt.Seats.FindIndex(value => value.MobilePartyId == mobilePartyId);
            if (index < 0) return false;

            seat = attempt.Seats[index];
            if (attempt.Selections.TryGetValue(mobilePartyId, out var existing))
                selection = existing.ToArray();
            return true;
        }
    }

    public bool TrySelect(MapEvent mapEvent, MobileParty party, IReadOnlyList<HideoutTroopSelectionEntry> requested,
        out HideoutTroopSelectionEntry[] accepted)
    {
        accepted = Array.Empty<HideoutTroopSelectionEntry>();
        if (mapEvent == null || party == null || requested == null ||
            !objectManager.TryGetId(party, out var partyId))
            return false;

        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return false;
            var index = attempt.Seats.FindIndex(value => value.MobilePartyId == partyId);
            if (index < 0) return false;
            var seat = attempt.Seats[index];

            var healthy = new Dictionary<string, int>();
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || !objectManager.TryGetId(element.Character, out var characterId))
                    continue;

                var count = element.Number - element.WoundedNumber;
                if (characterId == seat.HeroCharacterId && ModConfigProvider.ModOptions.PlayerWoundedBattleEntry)
                    count = element.Number;
                healthy[characterId] = count;
            }

            if (!healthy.TryGetValue(seat.HeroCharacterId, out var heroes) || heroes < 1)
                return false;

            var selection = new List<HideoutTroopSelectionEntry> { new(seat.HeroCharacterId, 1) };
            var selectedCounts = new Dictionary<string, int>();
            var escorts = 0;
            foreach (var entry in requested)
            {
                if (string.IsNullOrEmpty(entry.CharacterId) || entry.Count < 1)
                    return false;
                if (entry.CharacterId == seat.HeroCharacterId)
                {
                    if (entry.Count != 1) return false;
                    continue;
                }

                selectedCounts.TryGetValue(entry.CharacterId, out var alreadySelected);
                if (!healthy.TryGetValue(entry.CharacterId, out var available) ||
                    entry.Count > available - alreadySelected || entry.Count > seat.EscortAllowance - escorts)
                    return false;

                selectedCounts[entry.CharacterId] = alreadySelected + entry.Count;
                escorts += entry.Count;
                selection.Add(entry);
            }

            accepted = selection.ToArray();
            attempt.Selections[partyId] = accepted.ToArray();
            return true;
        }
    }

    public void RemoveSeat(MapEvent mapEvent, string mobilePartyId)
    {
        lock (gate)
        {
            if (mapEvent == null || !attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return;
            attempt.Seats.RemoveAll(seat => seat.MobilePartyId == mobilePartyId);
            attempt.Selections.Remove(mobilePartyId);
        }
    }

    public bool HaveAllAnswered(MapEvent mapEvent)
    {
        lock (gate)
            return mapEvent != null && attempts.TryGetValue(mapEvent, out var attempt) &&
                   attempt.Seats.All(seat => attempt.Selections.ContainsKey(seat.MobilePartyId));
    }

    public void Lock(MapEvent mapEvent)
    {
        lock (gate)
        {
            if (mapEvent == null || !attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return;
            attempt.Locked = true;

            // Vanilla's hall supplier draws its defender list from every mobile party on the side in order.
            var remaining = new Dictionary<string, int>(attempt.DefenderBudget);
            foreach (var mapEventParty in mapEvent.DefenderSide.Parties)
            {
                var party = mapEventParty.Party;
                if (party?.IsMobile != true) continue;

                var allocation = new Dictionary<string, int>();
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    if (element.Character == null || !objectManager.TryGetId(element.Character, out var characterId) ||
                        !remaining.TryGetValue(characterId, out var budget) || budget <= 0)
                        continue;

                    allocation.TryGetValue(characterId, out var already);
                    var take = Math.Min(budget, element.Number - element.WoundedNumber);
                    if (take <= 0) continue;
                    allocation[characterId] = already + take;
                    remaining[characterId] = budget - take;
                }
                attempt.DefenderAllocations[party] = allocation;
            }
        }
    }

    public bool IsAdmitted(MapEvent mapEvent, MobileParty party)
    {
        if (mapEvent == null || party == null || !objectManager.TryGetId(party, out var partyId)) return false;
        lock (gate)
            return attempts.TryGetValue(mapEvent, out var attempt) && attempt.Locked &&
                   attempt.Selections.ContainsKey(partyId);
    }

    public void FilterReserve(MapEvent mapEvent, MapEventParty party, List<TroopReserveEntry> entries)
    {
        lock (gate)
        {
            if (mapEvent == null || party?.Party == null || !attempts.TryGetValue(mapEvent, out var attempt) ||
                !attempt.Locked)
                return;

            Dictionary<string, int> allowed;
            if (party.Party.Side == BattleSideEnum.Defender)
            {
                attempt.DefenderAllocations.TryGetValue(party.Party, out allowed);
            }
            else
            {
                allowed = null;
                var mobileParty = party.Party.MobileParty;
                if (mobileParty != null && objectManager.TryGetId(mobileParty, out var partyId) &&
                    attempt.Selections.TryGetValue(partyId, out var selection))
                {
                    allowed = new Dictionary<string, int>();
                    foreach (var entry in selection)
                    {
                        allowed.TryGetValue(entry.CharacterId, out var count);
                        allowed[entry.CharacterId] = count + entry.Count;
                    }
                }
            }

            if (allowed == null)
            {
                entries.Clear();
                return;
            }

            var left = new Dictionary<string, int>(allowed);
            entries.RemoveAll(entry =>
            {
                if (!left.TryGetValue(entry.CharacterId, out var count) || count <= 0) return true;
                left[entry.CharacterId] = count - 1;
                return false;
            });
        }
    }

    public IReadOnlyList<MapEvent> GetOpenMapEvents()
    {
        lock (gate)
            return attempts.Keys.ToArray();
    }

    public void Forget(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        lock (gate)
            attempts.Remove(mapEvent);
    }

    /// <summary>
    /// Shares <paramref name="escortPlaces"/> out one at a time in seat order, skipping a seat once it has no troop
    /// left to bring, so the places spread as evenly as each party's healthy troops allow.
    /// </summary>
    internal static int[] ShareEscortPlaces(int escortPlaces, IReadOnlyList<int> availableTroops)
    {
        var shares = new int[availableTroops.Count];
        var placed = true;
        while (escortPlaces > 0 && placed)
        {
            placed = false;
            for (var seat = 0; seat < shares.Length && escortPlaces > 0; seat++)
            {
                if (shares[seat] >= availableTroops[seat]) continue;
                shares[seat]++;
                escortPlaces--;
                placed = true;
            }
        }
        return shares;
    }
}
