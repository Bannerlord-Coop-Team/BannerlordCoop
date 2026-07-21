using GameInterface.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// Server-authoritative reserve of troops yet to spawn in a coop battle, keyed by map event then by party.
/// The server builds each party's ordered <see cref="TroopReserveEntry"/> list once at battle start and
/// hands it to whichever client owns that party; that client's custom troop supplier spawns from it and
/// reports its progress back via <see cref="ReportSupplied"/>. The stored pointer is what makes disconnect
/// and host migration seamless: a new owner is given <see cref="GetRemaining"/> from the current pointer,
/// so the reserve survives the machine that left.
/// </summary>
public interface IBattleTroopLedger : IGameAbstraction
{
    /// <summary>Record (or replace) a party's full ordered reserve, resetting its supplied pointer to 0.</summary>
    void SetReserve(string mapEventId, string partyId, IReadOnlyList<TroopReserveEntry> entries);

    /// <summary>The party's full reserve and how many of it have been supplied so far.</summary>
    bool TryGetReserve(string mapEventId, string partyId, out IReadOnlyList<TroopReserveEntry> entries, out int suppliedCount);

    /// <summary>Advance a party's supplied pointer (monotonic, clamped to the reserve size).</summary>
    void ReportSupplied(string mapEventId, string partyId, int suppliedCount);

    /// <summary>Reset one party's supplied pointer while retaining its stable reserve identities and departures.</summary>
    void ResetSupplied(string mapEventId, string partyId);

    /// <summary>Remember one supplied troop that permanently left this mission without a roster casualty.</summary>
    void ReportDeparted(string mapEventId, string partyId, int troopSeed);

    /// <summary>Exact departed troop seeds that a future owner must not recover.</summary>
    IReadOnlyList<int> GetDepartedSeeds(string mapEventId, string partyId);

    /// <summary>The party's not-yet-supplied troops, from the current pointer onward (for a new owner).</summary>
    IReadOnlyList<TroopReserveEntry> GetRemaining(string mapEventId, string partyId);

    /// <summary>The party ids that have a reserve in this battle.</summary>
    IReadOnlyList<string> GetParties(string mapEventId);

    /// <summary>Drop the whole battle's reserve (on battle end).</summary>
    void Remove(string mapEventId);

    /// <summary>Drop a single party's reserve — e.g. its owner retreated, so a rejoin re-flattens it fresh
    /// (with a reset supplied pointer) instead of inheriting the spent one.</summary>
    void RemoveParty(string mapEventId, string partyId);
}

/// <inheritdoc cref="IBattleTroopLedger"/>
public class BattleTroopLedger : IBattleTroopLedger
{
    private sealed class PartyReserve
    {
        public TroopReserveEntry[] Entries = Array.Empty<TroopReserveEntry>();
        public int Supplied;
        public readonly HashSet<int> DepartedSeeds = new();
    }

    // mapEventId -> partyId -> reserve. Written from the server's battle-setup/supply handlers, read on
    // distribution and migration — a single lock keeps it consistent.
    private readonly Dictionary<string, Dictionary<string, PartyReserve>> battles = new();
    private readonly object gate = new();

    public void SetReserve(string mapEventId, string partyId, IReadOnlyList<TroopReserveEntry> entries)
    {
        lock (gate)
        {
            if (!battles.TryGetValue(mapEventId, out var parties))
            {
                parties = new Dictionary<string, PartyReserve>();
                battles[mapEventId] = parties;
            }

            parties[partyId] = new PartyReserve { Entries = entries?.ToArray() ?? Array.Empty<TroopReserveEntry>() };
        }
    }

    public bool TryGetReserve(string mapEventId, string partyId, out IReadOnlyList<TroopReserveEntry> entries, out int suppliedCount)
    {
        lock (gate)
        {
            if (battles.TryGetValue(mapEventId, out var parties) && parties.TryGetValue(partyId, out var reserve))
            {
                entries = reserve.Entries;
                suppliedCount = reserve.Supplied;
                return true;
            }

            entries = Array.Empty<TroopReserveEntry>();
            suppliedCount = 0;
            return false;
        }
    }

    public void ReportSupplied(string mapEventId, string partyId, int suppliedCount)
    {
        lock (gate)
        {
            if (!battles.TryGetValue(mapEventId, out var parties) || !parties.TryGetValue(partyId, out var reserve))
                return;

            // Monotonic and clamped: a stale/duplicate report can never rewind the pointer or run past the end.
            var clamped = Math.Min(reserve.Entries.Length, Math.Max(0, suppliedCount));
            if (clamped > reserve.Supplied)
                reserve.Supplied = clamped;
        }
    }

    public void ResetSupplied(string mapEventId, string partyId)
    {
        lock (gate)
        {
            if (battles.TryGetValue(mapEventId, out var parties)
                && parties.TryGetValue(partyId, out var reserve))
                reserve.Supplied = 0;
        }
    }

    public void ReportDeparted(string mapEventId, string partyId, int troopSeed)
    {
        lock (gate)
        {
            if (!battles.TryGetValue(mapEventId, out var parties)
                || !parties.TryGetValue(partyId, out var reserve))
                return;

            foreach (var entry in reserve.Entries)
            {
                if (entry.Seed != troopSeed) continue;
                reserve.DepartedSeeds.Add(troopSeed);
                return;
            }
        }
    }

    public IReadOnlyList<int> GetDepartedSeeds(string mapEventId, string partyId)
    {
        lock (gate)
        {
            if (!battles.TryGetValue(mapEventId, out var parties)
                || !parties.TryGetValue(partyId, out var reserve))
                return Array.Empty<int>();

            return reserve.DepartedSeeds.ToArray();
        }
    }

    public IReadOnlyList<TroopReserveEntry> GetRemaining(string mapEventId, string partyId)
    {
        lock (gate)
        {
            if (!battles.TryGetValue(mapEventId, out var parties) || !parties.TryGetValue(partyId, out var reserve))
                return Array.Empty<TroopReserveEntry>();

            int start = reserve.Supplied;
            int count = reserve.Entries.Length - start;
            if (count <= 0)
                return Array.Empty<TroopReserveEntry>();

            var slice = new TroopReserveEntry[count];
            Array.Copy(reserve.Entries, start, slice, 0, count);
            return slice;
        }
    }

    public IReadOnlyList<string> GetParties(string mapEventId)
    {
        lock (gate)
        {
            return battles.TryGetValue(mapEventId, out var parties)
                ? new List<string>(parties.Keys)
                : (IReadOnlyList<string>)Array.Empty<string>();
        }
    }

    public void Remove(string mapEventId)
    {
        lock (gate)
        {
            battles.Remove(mapEventId);
        }
    }

    public void RemoveParty(string mapEventId, string partyId)
    {
        lock (gate)
        {
            if (battles.TryGetValue(mapEventId, out var parties))
                parties.Remove(partyId);
        }
    }
}
