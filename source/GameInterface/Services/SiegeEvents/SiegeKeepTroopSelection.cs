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

/// <summary>What the battle path reads of the keep roster; its writes go through <see cref="ISiegeKeepStage"/>.</summary>
public interface ISiegeKeepRoster
{
    bool IsLocked(MapEvent mapEvent);
    bool IsAdmitted(MapEvent mapEvent, MobileParty party);
    void FilterReserve(MapEvent mapEvent, MapEventParty party, List<TroopReserveEntry> entries);
}

public interface ISiegeKeepTroopSelection : IGameAbstraction, ISiegeKeepRoster
{
    /// <summary>Opens the keep stage with its seated players and the defender troops vanilla puts in the hall.</summary>
    void Open(MapEvent mapEvent, IReadOnlyList<SiegeKeepSeat> seats, IReadOnlyDictionary<string, int> defenderBudget,
        DateTime deadlineUtc);
    bool IsOpen(MapEvent mapEvent);
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
            if (!rules.TryAccept(healthy, seat.HeroCharacterId, seat.EscortAllowance, requested, out var accepted, out _))
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
        var defendingParties = new List<PartyBase>();
        var healthyByParty = new List<IReadOnlyDictionary<string, int>>();
        foreach (var mapEventParty in mapEvent.DefenderSide.Parties)
        {
            var party = mapEventParty.Party;
            if (party?.IsMobile != true) continue;
            defendingParties.Add(party);
            healthyByParty.Add(rules.CountHealthy(party.MobileParty, heroCharacterId: null));
        }

        lock (gate)
        {
            if (!attempts.TryGetValue(mapEvent, out var attempt) || attempt.Locked) return;
            attempt.Locked = true;

            // One allocation per defending party, in the same order.
            var allocations = SplitDefenders(attempt.DefenderBudget, healthyByParty);
            for (var index = 0; index < defendingParties.Count; index++)
                attempt.DefenderAllocations[defendingParties[index]] = allocations[index];
        }
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

            rules.TrimReserve(entries, GetChosenTroops(attempt, party.Party));
        }
    }

    // The troops a party fields in the keep, none when the party has no part in it.
    private IEnumerable<HideoutTroopSelectionEntry> GetChosenTroops(Attempt attempt, PartyBase party)
    {
        if (party.Side == BattleSideEnum.Defender)
        {
            if (!attempt.DefenderAllocations.TryGetValue(party, out var allocation))
                return Array.Empty<HideoutTroopSelectionEntry>();
            return allocation.Select(pair => new HideoutTroopSelectionEntry(pair.Key, pair.Value));
        }

        if (party.MobileParty == null || !objectManager.TryGetId(party.MobileParty, out var partyId) ||
            !attempt.Selections.TryGetValue(partyId, out var selection))
            return Array.Empty<HideoutTroopSelectionEntry>();

        return selection;
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
    /// Splits the hall's defender budget greedily across the defending parties in order, each taking what its healthy
    /// troops of a character allow.
    /// </summary>
    internal static List<Dictionary<string, int>> SplitDefenders(IReadOnlyDictionary<string, int> budget,
        IReadOnlyList<IReadOnlyDictionary<string, int>> healthyByParty)
    {
        var remaining = budget.ToDictionary(pair => pair.Key, pair => pair.Value);
        var allocations = new List<Dictionary<string, int>>();
        foreach (var party in healthyByParty)
        {
            var allocation = new Dictionary<string, int>();
            foreach (var pair in party)
            {
                var (characterId, healthy) = (pair.Key, pair.Value);
                if (!remaining.TryGetValue(characterId, out var left) || left <= 0) continue;
                var take = Math.Min(left, healthy);
                if (take <= 0) continue;
                allocation[characterId] = take;
                remaining[characterId] = left - take;
            }
            allocations.Add(allocation);
        }
        return allocations;
    }
}
