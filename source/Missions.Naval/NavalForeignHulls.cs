using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using System.Collections.Generic;
using TaleWorlds.Engine;

namespace Missions.Naval;

/// <summary>
/// [Game thread writes] The hulls in the current mission that another owner simulates. Static because the Harmony
/// gates read it; hulls that never registered here stay on the vanilla path. Copy on write,
/// because the ship damage gate and the crew load patch also read it on physics threads.
/// </summary>
internal static class NavalForeignHulls
{
    private static volatile HashSet<MissionShip> hulls = new HashSet<MissionShip>();
    // Each copy by its hull's physics script, for the crew load patch, which only sees the NavalPhysics.
    private static volatile Dictionary<NavalPhysics, SteeredCopy> byPhysics = new Dictionary<NavalPhysics, SteeredCopy>();

    public static IEnumerable<MissionShip> All => hulls;

    /// <summary>Marks a hull as a copy, reachable from its physics script for the crew load patch.</summary>
    public static void Add(MissionShip ship, SteeredCopy copy)
    {
        hulls = new HashSet<MissionShip>(hulls) { ship };
        if (ship.Physics == null) return;

        byPhysics = new Dictionary<NavalPhysics, SteeredCopy>(byPhysics) { [ship.Physics] = copy };
    }

    /// <summary>Puts a hull back on the vanilla path.</summary>
    public static void Remove(MissionShip ship)
    {
        var remaining = new HashSet<MissionShip>(hulls);
        remaining.Remove(ship);
        hulls = remaining;
        if (ship.Physics == null) return;

        var remainingCopies = new Dictionary<NavalPhysics, SteeredCopy>(byPhysics);
        remainingCopies.Remove(ship.Physics);
        byPhysics = remainingCopies;
    }

    public static bool Contains(MissionShip ship) => ship != null && hulls.Contains(ship);

    /// <summary>[Any thread] The copy whose hull runs this physics script; false for an own hull's.</summary>
    public static bool TryGetCopy(NavalPhysics physics, out SteeredCopy copy)
    {
        copy = null;
        return physics != null && byPhysics.TryGetValue(physics, out copy);
    }

    /// <summary>[Any thread] Whether the entity belongs to a copy: its root is a copy's hull entity.</summary>
    public static bool ContainsRootOf(WeakGameEntity entity)
    {
        if (!entity.IsValid) return false;

        var root = entity.Root;
        foreach (var ship in hulls)
        {
            if (ship.GameEntity == root) return true;
        }

        return false;
    }

    /// <summary>Forgets every copy when the battle ends.</summary>
    public static void Clear()
    {
        hulls = new HashSet<MissionShip>();
        byPhysics = new Dictionary<NavalPhysics, SteeredCopy>();
    }
}
