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
    bool TryGetSeat(MapEvent mapEvent, string mobilePartyId, out SiegeKeepSeat seat);
    /// <summary>Whether the seated player already chose its squad.</summary>
    bool HasAnswered(MapEvent mapEvent, string mobilePartyId);
    bool TrySelect(MapEvent mapEvent, MobileParty party, IReadOnlyList<HideoutTroopSelectionEntry> requested);
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
    private readonly ITroopSelectionRules rules;
    private readonly Dictionary<MapEvent, Attempt> attempts = new();
    private readonly object gate = new();

    public SiegeKeepTroopSelection(IObjectManager objectManager, ITroopSelectionRules rules)
    {
        this.objectManager = objectManager;
        this.rules = rules;
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
            return attempts.ContainsKey(mapEvent);
    }

    public bool IsLocked(MapEvent mapEvent)
    {
        lock (gate)
            return attempts.TryGetValue(mapEvent, out var attempt) && attempt.Locked;
    }

    public bool TryGetDeadline(MapEvent mapEvent, out DateTime deadlineUtc)
    {
        deadlineUtc = default;
        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt)) return false;
            deadlineUtc = attempt.DeadlineUtc;
            return true;
        }
    }

    public IReadOnlyList<SiegeKeepSeat> GetSeats(MapEvent mapEvent)
    {
        lock (gate)
            return attempts.TryGetValue(mapEvent, out var attempt)
                ? attempt.Seats.ToArray()
                : Array.Empty<SiegeKeepSeat>();
    }

    public bool TryGetSeat(MapEvent mapEvent, string mobilePartyId, out SiegeKeepSeat seat)
    {
        seat = default;
        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt)) return false;
            var index = attempt.Seats.FindIndex(value => value.MobilePartyId == mobilePartyId);
            if (index < 0) return false;

            seat = attempt.Seats[index];
            return true;
        }
    }

    public bool HasAnswered(MapEvent mapEvent, string mobilePartyId)
    {
        lock (gate)
            return attempts.TryGetValue(mapEvent, out var attempt) && attempt.Selections.ContainsKey(mobilePartyId);
    }

    public bool TrySelect(MapEvent mapEvent, MobileParty party, IReadOnlyList<HideoutTroopSelectionEntry> requested)
    {
        if (!objectManager.TryGetId(party, out var partyId)) return false;

        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return false;
            var index = attempt.Seats.FindIndex(value => value.MobilePartyId == partyId);
            if (index < 0) return false;
            var seat = attempt.Seats[index];

            var healthy = rules.CountHealthy(party, seat.HeroCharacterId);
            if (!rules.TryAccept(healthy, seat.HeroCharacterId, seat.EscortAllowance, requested, out var accepted))
                return false;

            attempt.Selections[partyId] = accepted;
            return true;
        }
    }

    public void RemoveSeat(MapEvent mapEvent, string mobilePartyId)
    {
        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return;
            attempt.Seats.RemoveAll(seat => seat.MobilePartyId == mobilePartyId);
            attempt.Selections.Remove(mobilePartyId);
        }
    }

    public bool HaveAllAnswered(MapEvent mapEvent)
    {
        lock (gate)
            return attempts.TryGetValue(mapEvent, out var attempt) &&
                   attempt.Seats.All(seat => attempt.Selections.ContainsKey(seat.MobilePartyId));
    }

    public void Lock(MapEvent mapEvent)
    {
        // Vanilla's hall supplier draws its defender list from every mobile party on the side in order.
        var defendingParties = mapEvent.DefenderSide.Parties
            .Select(mapEventParty => mapEventParty.Party)
            .Where(party => party?.IsMobile == true)
            .ToList();
        var healthyByParty = defendingParties.Select(CountHealthyDefenders).ToList();

        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return;
            attempt.Locked = true;

            var allocations = SplitDefenders(attempt.DefenderBudget, healthyByParty);
            for (var index = 0; index < defendingParties.Count; index++)
                attempt.DefenderAllocations[defendingParties[index]] = allocations[index];
        }
    }

    private IReadOnlyList<(string CharacterId, int Healthy)> CountHealthyDefenders(PartyBase party)
    {
        var troops = new List<(string, int)>();
        foreach (var element in party.MemberRoster.GetTroopRoster())
            if (element.Character != null && objectManager.TryGetId(element.Character, out var characterId))
                troops.Add((characterId, element.Number - element.WoundedNumber));
        return troops;
    }

    public bool IsAdmitted(MapEvent mapEvent, MobileParty party)
    {
        if (!objectManager.TryGetId(party, out var partyId)) return false;
        lock (gate)
            return attempts.TryGetValue(mapEvent, out var attempt) && attempt.Locked &&
                   attempt.Selections.ContainsKey(partyId);
    }

    public void FilterReserve(MapEvent mapEvent, MapEventParty party, List<TroopReserveEntry> entries)
    {
        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt) || !attempt.Locked) return;

            var allowed = GetAllowedTroops(attempt, party.Party);
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

    // The troops a party may field in the keep, or null when the party has no part in it.
    private Dictionary<string, int> GetAllowedTroops(Attempt attempt, PartyBase party)
    {
        if (party.Side == BattleSideEnum.Defender)
            return attempt.DefenderAllocations.TryGetValue(party, out var allocation) ? allocation : null;

        if (party.MobileParty == null || !objectManager.TryGetId(party.MobileParty, out var partyId) ||
            !attempt.Selections.TryGetValue(partyId, out var selection))
            return null;

        var allowed = new Dictionary<string, int>();
        foreach (var entry in selection)
        {
            allowed.TryGetValue(entry.CharacterId, out var count);
            allowed[entry.CharacterId] = count + entry.Count;
        }
        return allowed;
    }

    public IReadOnlyList<MapEvent> GetOpenMapEvents()
    {
        lock (gate)
            return attempts.Keys.ToArray();
    }

    public void Forget(MapEvent mapEvent)
    {
        lock (gate)
            attempts.Remove(mapEvent);
    }

    /// <summary>
    /// Vanilla's hall attacker places for <paramref name="defenderCount"/> defenders, with a seat for each player in
    /// order while places last and the rest shared as escorts. Returns each seated player's escort allowance.
    /// </summary>
    internal static int[] PlanSeats(int defenderCount, float attackerDefenderRatio, int maxAttackers,
        IReadOnlyList<int> escortsPerCandidate)
    {
        var attackerPlaces = Math.Max(1, Math.Min(maxAttackers,
            TaleWorlds.Library.MathF.Round(defenderCount * attackerDefenderRatio)));
        var seated = Math.Min(attackerPlaces, escortsPerCandidate.Count);
        return ShareEscortPlaces(attackerPlaces - seated, escortsPerCandidate.Take(seated).ToList());
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

    /// <summary>
    /// Splits the hall's defender budget greedily across the defending parties in order, each taking what its healthy
    /// troops of a character allow.
    /// </summary>
    internal static List<Dictionary<string, int>> SplitDefenders(IReadOnlyDictionary<string, int> budget,
        IReadOnlyList<IReadOnlyList<(string CharacterId, int Healthy)>> healthyByParty)
    {
        var remaining = budget.ToDictionary(pair => pair.Key, pair => pair.Value);
        var allocations = new List<Dictionary<string, int>>();
        foreach (var party in healthyByParty)
        {
            var allocation = new Dictionary<string, int>();
            foreach (var (characterId, healthy) in party)
            {
                if (!remaining.TryGetValue(characterId, out var left) || left <= 0) continue;
                var take = Math.Min(left, healthy);
                if (take <= 0) continue;
                allocation.TryGetValue(characterId, out var already);
                allocation[characterId] = already + take;
                remaining[characterId] = left - take;
            }
            allocations.Add(allocation);
        }
        return allocations;
    }
}
