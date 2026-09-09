using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// Bridges the network handler (which receives a side's reserve from the server) to the
/// <see cref="CoopTroopSupplier"/>s the injection patch installs into the mission. A reserve message can
/// arrive before or after the mission (and thus the supplier) is built, so a reserve that arrives early is
/// buffered (latest wins) and applied when the matching supplier registers. Static because the injection
/// patch can't resolve DI services.
/// </summary>
public static class CoopTroopSupplierRegistry
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, CoopTroopSupplier> Suppliers = new Dictionary<string, CoopTroopSupplier>();
    private static readonly Dictionary<string, (PartyReserve[] Reserve, int SideTotal, int PlayerParties, long AllocationRevision, int BattleSize)> Pending =
        new Dictionary<string, (PartyReserve[], int, int, long, int)>();

    private static string Key(string mapEventId, BattleSideEnum side) => mapEventId + "|" + (int)side;

    /// <summary>[Game thread] A supplier was installed into the mission; apply any reserve buffered for it.</summary>
    public static void Register(CoopTroopSupplier supplier)
    {
        lock (Gate)
        {
            var key = Key(supplier.MapEventId, supplier.Side);
            Suppliers[key] = supplier;

            if (Pending.TryGetValue(key, out var buffered))
            {
                supplier.SetReserve(buffered.Reserve, buffered.SideTotal, buffered.PlayerParties,
                    buffered.BattleSize, buffered.AllocationRevision);
                Pending.Remove(key);
            }
        }
    }

    /// <summary>[Network thread] Set a side's reserve (full authoritative set), or buffer it until a supplier
    /// exists. Returns the final local pointers of the parties the REPLACE dropped (the BR-033 flush payload;
    /// see <see cref="CoopTroopSupplier.SetReserve"/>) — empty when buffered: with no supplier, nothing was
    /// ever supplied locally, so there is nothing beyond the server's own ledger to flush.</summary>
    /// <param name="sideTotalTroops">Every troop on this side across all owners.</param>
    public static IReadOnlyList<(string PartyId, int Supplied)> Feed(string mapEventId, BattleSideEnum side,
        PartyReserve[] reserve, int sideTotalTroops, int playerOwnedPartyCount, long allocationRevision,
        int battleSize)
    {
        lock (Gate)
        {
            var key = Key(mapEventId, side);
            if (Suppliers.TryGetValue(key, out var supplier))
                return supplier.SetReserve(reserve, sideTotalTroops, playerOwnedPartyCount, battleSize,
                    allocationRevision);

            Pending[key] = (reserve, sideTotalTroops, playerOwnedPartyCount, allocationRevision, battleSize);
            return Array.Empty<(string, int)>();
        }
    }

    /// <summary>The suppliers installed for a battle (used to read supplied pointers for progress reporting).</summary>
    public static IReadOnlyList<CoopTroopSupplier> GetSuppliers(string mapEventId)
    {
        lock (Gate)
        {
            var prefix = mapEventId + "|";
            var result = new List<CoopTroopSupplier>();
            foreach (var pair in Suppliers)
                if (pair.Key.StartsWith(prefix))
                    result.Add(pair.Value);
            return result;
        }
    }

    /// <summary>Drop everything for a battle (on mission end).</summary>
    public static void ClearBattle(string mapEventId)
    {
        lock (Gate)
        {
            var prefix = mapEventId + "|";
            foreach (var key in new List<string>(Suppliers.Keys))
                if (key.StartsWith(prefix)) Suppliers.Remove(key);
            foreach (var key in new List<string>(Pending.Keys))
                if (key.StartsWith(prefix)) Pending.Remove(key);
        }
    }

#if DEBUG
    /// <summary>Read-only local supplier and buffered-reserve state for a baseline diagnostic.</summary>
    public static CoopTroopSupplierDebugState CaptureDebugState(string mapEventId)
    {
        var state = new CoopTroopSupplierDebugState
        {
            MapEventId = mapEventId,
        };
        if (string.IsNullOrWhiteSpace(mapEventId))
            return state;

        lock (Gate)
        {
            string prefix = mapEventId + "|";
            foreach (var pair in Suppliers)
            {
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                CoopTroopSupplier supplier = pair.Value;
                try
                {
                    CoopTroopSupplier.DebugSnapshot snapshot = supplier.CaptureDebugSnapshot();
                    var side = new CoopTroopSupplierDebugSideState
                    {
                        Side = supplier.Side.ToString(),
                        Populated = snapshot.Populated,
                        PlayerPartyId = snapshot.PlayerPartyId,
                        ReserveRevision = snapshot.ReserveRevision,
                        AllocationRevision = snapshot.AllocationRevision,
                        BattleSize = snapshot.BattleSize,
                        SideTotalTroops = snapshot.SideTotalTroops,
                        OwnedTotalTroops = snapshot.EntryCount,
                        OwnedSuppliedTroops = snapshot.SuppliedCount,
                        OwnedRemainingTroops = snapshot.RemainingCount,
                        EntryCount = snapshot.EntryCount,
                        SuppliedCount = snapshot.SuppliedCount,
                        RemainingCount = snapshot.RemainingCount,
                    };
                    foreach (CoopTroopSupplier.DebugPartySnapshot party in snapshot.Parties)
                    {
                        side.Parties.Add(new CoopTroopSupplierDebugPartyState
                        {
                            Side = side.Side,
                            PartyId = party.PartyId,
                            EntryCount = party.EntryCount,
                            SuppliedCount = party.SuppliedCount,
                            RemainingCount = party.RemainingCount,
                            RemainingTroops = party.RemainingCount,
                            ReserveRevision = side.ReserveRevision,
                            AllocationRevision = side.AllocationRevision,
                        });
                    }
                    state.Suppliers.Add(side);
                }
                catch (Exception e)
                {
                    state.Errors.Add(new CoopTroopSupplierDebugError
                    {
                        Side = supplier.Side.ToString(),
                        Source = "supplier",
                        Error = e.GetType().Name + ": " + e.Message,
                    });
                }
            }

            foreach (var pair in Pending)
            {
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal) ||
                    !int.TryParse(pair.Key.Substring(prefix.Length), out int sideValue))
                {
                    continue;
                }

                var pending = new CoopTroopSupplierDebugPendingState
                {
                    Side = ((BattleSideEnum)sideValue).ToString(),
                    PartyCount = pair.Value.Reserve?.Length ?? 0,
                    ReserveRevision = null,
                    ReserveRevisionError = "supplier-unavailable",
                    SideTotalTroops = pair.Value.SideTotal,
                    PlayerOwnedPartyCount = pair.Value.PlayerParties,
                    AllocationRevision = pair.Value.AllocationRevision,
                    BattleSize = pair.Value.BattleSize,
                };
                int entryCount = 0;
                int suppliedCount = 0;
                foreach (PartyReserve party in pair.Value.Reserve ?? Array.Empty<PartyReserve>())
                {
                    var entries = party.Entries ?? Array.Empty<TroopReserveEntry>();
                    int effectiveSuppliedCount = Math.Min(Math.Max(0, party.SuppliedCount), entries.Length);
                    entryCount += entries.Length;
                    suppliedCount += effectiveSuppliedCount;
                    pending.Parties.Add(new CoopTroopSupplierDebugPendingPartyState
                    {
                        Side = pending.Side,
                        PartyId = party.PartyId,
                        EntryCount = entries.Length,
                        RawSuppliedCount = party.SuppliedCount,
                        EffectiveSuppliedCount = effectiveSuppliedCount,
                        RemainingCount = entries.Length - effectiveSuppliedCount,
                        LocalSuppliedCount = null,
                        LocalSuppliedCountError = "supplier-unavailable",
                        AllocationRevision = pair.Value.AllocationRevision,
                    });
                }
                pending.EntryCount = entryCount;
                pending.SuppliedCount = suppliedCount;
                pending.RemainingCount = entryCount - suppliedCount;
                state.Pending.Add(pending);
            }
        }

        state.Suppliers.Sort((left, right) => string.CompareOrdinal(left.Side, right.Side));
        state.Pending.Sort((left, right) => string.CompareOrdinal(left.Side, right.Side));
        return state;
    }

    /// <summary>Read-only supplier evidence for a specific returning party, retaining partial state on failures.</summary>
    public static CoopTroopSupplierDebugObservation CaptureDebugObservation(string mapEventId, string returningPartyId)
    {
        var observation = new CoopTroopSupplierDebugObservation
        {
            MapEventId = mapEventId,
            ReturningPartyId = returningPartyId,
        };

        if (string.IsNullOrWhiteSpace(mapEventId))
        {
            observation.SupplierStateError = "map-event-id-unavailable";
            return observation;
        }

        try
        {
            observation.SupplierState = CaptureDebugState(mapEventId);
        }
        catch (Exception e)
        {
            observation.SupplierStateError = e.GetType().Name + ": " + e.Message;
            return observation;
        }

        if (observation.SupplierState == null)
        {
            observation.SupplierStateError = "supplier-state-unavailable";
            return observation;
        }

        if (string.IsNullOrWhiteSpace(returningPartyId))
        {
            observation.ReturningPartyError = "returning-party-id-unavailable";
            return observation;
        }

        CoopTroopSupplierDebugPartyState match = null;
        foreach (CoopTroopSupplierDebugSideState side in observation.SupplierState.Suppliers)
        {
            foreach (CoopTroopSupplierDebugPartyState party in side.Parties)
            {
                if (!string.Equals(party.PartyId, returningPartyId, StringComparison.Ordinal))
                    continue;

                if (match != null)
                {
                    observation.ReturningPartyError = "returning-party-supplier-entry-ambiguous";
                    return observation;
                }
                match = party;
            }
        }

        observation.ReturningParty = match;
        if (match == null)
            observation.ReturningPartyError = "returning-party-supplier-entry-unavailable";
        return observation;
    }
#endif
}

#if DEBUG
public sealed class CoopTroopSupplierDebugState
{
    public string MapEventId { get; set; }
    public List<CoopTroopSupplierDebugSideState> Suppliers { get; } = new List<CoopTroopSupplierDebugSideState>();
    public List<CoopTroopSupplierDebugPendingState> Pending { get; } = new List<CoopTroopSupplierDebugPendingState>();
    public List<CoopTroopSupplierDebugError> Errors { get; } = new List<CoopTroopSupplierDebugError>();
}

public sealed class CoopTroopSupplierDebugObservation
{
    public string MapEventId { get; set; }
    public string ReturningPartyId { get; set; }
    public CoopTroopSupplierDebugState SupplierState { get; set; }
    public string SupplierStateError { get; set; }
    public CoopTroopSupplierDebugPartyState ReturningParty { get; set; }
    public string ReturningPartyError { get; set; }
}

public sealed class CoopTroopSupplierDebugError
{
    public string Side { get; set; }
    public string Source { get; set; }
    public string Error { get; set; }
}

public sealed class CoopTroopSupplierDebugSideState
{
    public string Side { get; set; }
    public bool Populated { get; set; }
    public string PlayerPartyId { get; set; }
    public int ReserveRevision { get; set; }
    public long AllocationRevision { get; set; }
    public int BattleSize { get; set; }
    public int SideTotalTroops { get; set; }
    public int OwnedTotalTroops { get; set; }
    public int OwnedSuppliedTroops { get; set; }
    public int OwnedRemainingTroops { get; set; }
    public int EntryCount { get; set; }
    public int SuppliedCount { get; set; }
    public int RemainingCount { get; set; }
    public List<CoopTroopSupplierDebugPartyState> Parties { get; } = new List<CoopTroopSupplierDebugPartyState>();
}

public sealed class CoopTroopSupplierDebugPartyState
{
    public string Side { get; set; }
    public string PartyId { get; set; }
    public int EntryCount { get; set; }
    public int SuppliedCount { get; set; }
    public int RemainingCount { get; set; }
    public int RemainingTroops { get; set; }
    public int ReserveRevision { get; set; }
    public long AllocationRevision { get; set; }
}

public sealed class CoopTroopSupplierDebugPendingState
{
    public string Side { get; set; }
    public int PartyCount { get; set; }
    public int EntryCount { get; set; }
    public int SuppliedCount { get; set; }
    public int RemainingCount { get; set; }
    public List<CoopTroopSupplierDebugPendingPartyState> Parties { get; } = new List<CoopTroopSupplierDebugPendingPartyState>();
    public int? ReserveRevision { get; set; }
    public string ReserveRevisionError { get; set; }
    public int SideTotalTroops { get; set; }
    public int PlayerOwnedPartyCount { get; set; }
    public long AllocationRevision { get; set; }
    public int BattleSize { get; set; }
}

public sealed class CoopTroopSupplierDebugPendingPartyState
{
    public string Side { get; set; }
    public string PartyId { get; set; }
    public int EntryCount { get; set; }
    public int RawSuppliedCount { get; set; }
    public int EffectiveSuppliedCount { get; set; }
    public int RemainingCount { get; set; }
    public int? LocalSuppliedCount { get; set; }
    public string LocalSuppliedCountError { get; set; }
    public long AllocationRevision { get; set; }
}
#endif
