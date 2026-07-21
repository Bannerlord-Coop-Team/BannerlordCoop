using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// Bridges the network handler (which receives a side's reserve from the server) to the
/// <see cref="CoopTroopSupplier"/>s the injection patch installs into the mission. A reserve message can
/// arrive before or after the mission (and thus the supplier) is built, so a reserve that arrives early is
/// buffered and applied when the matching supplier registers. A later full reserve replaces the earlier one,
/// including its persistent initial entitlements. Static because the injection patch can't resolve DI services.
/// </summary>
public static class CoopTroopSupplierRegistry
{
    private sealed class PendingReserve
    {
        public readonly PartyReserve[] Parties;
        public readonly long GrantGeneration;
        public readonly bool CompletesInitialSizing;

        public PendingReserve(PartyReserve[] parties, long grantGeneration, bool completesInitialSizing)
        {
            Parties = parties;
            GrantGeneration = grantGeneration;
            CompletesInitialSizing = completesInitialSizing;
        }
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, CoopTroopSupplier> Suppliers = new Dictionary<string, CoopTroopSupplier>();
    private static readonly Dictionary<string, PendingReserve> Pending = new Dictionary<string, PendingReserve>();

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
                supplier.SetReserve(buffered.Parties, buffered.GrantGeneration, buffered.CompletesInitialSizing);
                Pending.Remove(key);
            }
        }
    }

    /// <summary>[Network thread] Set a side's reserve (full authoritative set), or buffer it until a supplier
    /// exists. Returns the final local pointers of the parties the REPLACE dropped (the BR-033 flush payload;
    /// see <see cref="CoopTroopSupplier.SetReserve"/>) — empty when buffered: with no supplier, nothing was
    /// ever supplied locally, so there is nothing beyond the server's own ledger to flush.</summary>
    public static IReadOnlyList<(string PartyId, int Supplied)> Feed(
        string mapEventId,
        BattleSideEnum side,
        PartyReserve[] reserve,
        long grantGeneration = 0,
        bool completesInitialSizing = true)
    {
        lock (Gate)
        {
            var key = Key(mapEventId, side);
            if (Suppliers.TryGetValue(key, out var supplier))
                return supplier.SetReserve(reserve, grantGeneration, completesInitialSizing);

            Pending[key] = new PendingReserve(
                reserve ?? Array.Empty<PartyReserve>(),
                grantGeneration,
                completesInitialSizing);
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
}
