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
    private static readonly Dictionary<string, PartyReserve[]> Pending = new Dictionary<string, PartyReserve[]>();

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
                supplier.SetReserve(buffered);
                Pending.Remove(key);
            }
        }
    }

    /// <summary>[Network thread] Set a side's reserve (full authoritative set), or buffer it until a supplier exists.</summary>
    public static void Feed(string mapEventId, BattleSideEnum side, PartyReserve[] reserve)
    {
        lock (Gate)
        {
            var key = Key(mapEventId, side);
            if (Suppliers.TryGetValue(key, out var supplier))
                supplier.SetReserve(reserve);
            else
                Pending[key] = reserve; // latest wins
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
