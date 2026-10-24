using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;

namespace Missions.Naval;

/// <summary>
/// [Game thread writes] The hulls in the current mission that another owner simulates. Static because the Harmony
/// gates read it; hulls that never registered here stay on the vanilla path. Copy on write,
/// because the ship damage gate also reads it from hull contact callbacks on physics threads.
/// </summary>
internal static class NavalForeignHulls
{
    // One snapshot swapped in one write, so a physics-thread reader never sees a hull in one lookup and not the other.
    private static volatile Snapshot current = Snapshot.Empty;

    /// <summary>Every copy hull.</summary>
    public static IEnumerable<MissionShip> All => current.Hulls;

    /// <summary>Marks a hull as a copy, reachable from its physics script for the crew load patch.</summary>
    public static void Add(MissionShip ship, SteeredCopy copy)
    {
        var before = current;
        current = new Snapshot(
            new HashSet<MissionShip>(before.Hulls) { ship },
            new Dictionary<NavalPhysics, SteeredCopy>(before.ByPhysics) { [ship.Physics] = copy });
    }

    /// <summary>Puts a hull back on the vanilla path.</summary>
    public static void Remove(MissionShip ship)
    {
        var before = current;
        var hulls = new HashSet<MissionShip>(before.Hulls);
        hulls.Remove(ship);
        var byPhysics = new Dictionary<NavalPhysics, SteeredCopy>(before.ByPhysics);
        byPhysics.Remove(ship.Physics);
        current = new Snapshot(hulls, byPhysics);
    }

    /// <summary>[Any thread] Whether the hull is another owner's copy here.</summary>
    public static bool Contains(MissionShip ship) => ship != null && current.Hulls.Contains(ship);

    /// <summary>[Any thread] The copy whose hull runs this physics script; false for an own hull's.</summary>
    public static bool TryGetCopy(NavalPhysics physics, out SteeredCopy copy)
    {
        copy = null;
        return physics != null && current.ByPhysics.TryGetValue(physics, out copy);
    }

    /// <summary>[Any thread] Whether the entity belongs to a copy: its root is a copy's hull entity.</summary>
    public static bool ContainsRootOf(WeakGameEntity entity) => entity.IsValid && IsCopyRoot(entity.Root);

    /// <summary>[Any thread] Whether the root entity is a copy's hull entity.</summary>
    public static bool IsCopyRoot(WeakGameEntity root) => current.Roots.Contains(root.Pointer);

    /// <summary>Forgets every copy when the battle ends.</summary>
    public static void Clear() => current = Snapshot.Empty;

    /// <summary>The copy set and its lookups at one moment; never changed once published.</summary>
    private sealed class Snapshot
    {
        public static readonly Snapshot Empty =
            new Snapshot(new HashSet<MissionShip>(), new Dictionary<NavalPhysics, SteeredCopy>());

        /// <summary>A snapshot over collections nobody writes after this; the root lookup is built from the hulls.</summary>
        public Snapshot(HashSet<MissionShip> hulls, Dictionary<NavalPhysics, SteeredCopy> byPhysics)
        {
            Hulls = hulls;
            ByPhysics = byPhysics;
            Roots = new HashSet<UIntPtr>(hulls.Select(hull => hull.GameEntity.Pointer));
        }

        // Every copy hull.
        public HashSet<MissionShip> Hulls { get; }

        // Each copy by its hull's physics script, for the crew load patch, which only sees the NavalPhysics.
        public Dictionary<NavalPhysics, SteeredCopy> ByPhysics { get; }

        // Each copy's hull entity pointer, for the contact filter; keyed on the pointer, as WeakGameEntity compares by it.
        public HashSet<UIntPtr> Roots { get; }
    }
}
