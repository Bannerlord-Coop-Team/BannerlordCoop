using GameInterface;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;

namespace Missions.Naval;

/// <summary>
/// [Game thread writes] The hulls in this client's current mission that another owner simulates; hulls that never registered
/// here stay on the vanilla path. The Harmony gates read it, also from hull contact callbacks on physics threads.
/// </summary>
public interface INavalForeignHullRegistry
{
    /// <summary>Every copy hull.</summary>
    IEnumerable<MissionShip> All { get; }

    /// <summary>Marks a hull as a copy, reachable from its physics script for the crew load patch.</summary>
    void Add(MissionShip ship, SteeredCopy copy);

    /// <summary>Puts a hull back on the vanilla path.</summary>
    void Remove(MissionShip ship);

    /// <summary>[Any thread] Whether the hull is another owner's copy here.</summary>
    bool Contains(MissionShip ship);

    /// <summary>[Any thread] The copy whose hull runs this physics script; false for an own hull's.</summary>
    bool TryGetCopy(NavalPhysics physics, out SteeredCopy copy);

    /// <summary>[Any thread] Whether the entity belongs to a copy: its root is a copy's hull entity.</summary>
    bool ContainsRootOf(WeakGameEntity entity);

    /// <summary>[Any thread] Whether the root entity is a copy's hull entity.</summary>
    bool IsCopyRoot(WeakGameEntity root);

    /// <summary>Forgets every copy when the battle ends.</summary>
    void Clear();
}

/// <summary>
/// One per client container. Copy on write, so a physics-thread reader sees one consistent snapshot of the copy set.
/// </summary>
internal class NavalForeignHullRegistry : INavalForeignHullRegistry
{
    // One snapshot swapped in one write, so a physics-thread reader never sees a hull in one lookup and not the other.
    private volatile Snapshot current = Snapshot.Empty;

    /// <inheritdoc/>
    public IEnumerable<MissionShip> All => current.Hulls;

    /// <inheritdoc/>
    public void Add(MissionShip ship, SteeredCopy copy)
    {
        var before = current;
        current = new Snapshot(
            new HashSet<MissionShip>(before.Hulls) { ship },
            new Dictionary<NavalPhysics, SteeredCopy>(before.ByPhysics) { [ship.Physics] = copy });
    }

    /// <inheritdoc/>
    public void Remove(MissionShip ship)
    {
        var before = current;
        var hulls = new HashSet<MissionShip>(before.Hulls);
        hulls.Remove(ship);
        var byPhysics = new Dictionary<NavalPhysics, SteeredCopy>(before.ByPhysics);
        byPhysics.Remove(ship.Physics);
        current = new Snapshot(hulls, byPhysics);
    }

    /// <inheritdoc/>
    public bool Contains(MissionShip ship) => ship != null && current.Hulls.Contains(ship);

    /// <inheritdoc/>
    public bool TryGetCopy(NavalPhysics physics, out SteeredCopy copy)
    {
        copy = null;
        return physics != null && current.ByPhysics.TryGetValue(physics, out copy);
    }

    /// <inheritdoc/>
    public bool ContainsRootOf(WeakGameEntity entity) => entity.IsValid && IsCopyRoot(entity.Root);

    /// <inheritdoc/>
    public bool IsCopyRoot(WeakGameEntity root) => current.Roots.Contains(root.Pointer);

    /// <inheritdoc/>
    public void Clear() => current = Snapshot.Empty;

    /// <summary>[Any thread] Whether the hull is a copy in the client container patches resolve from; false when none resolves.</summary>
    internal static bool IsCopyHere(MissionShip ship) =>
        ContainerProvider.TryResolve<INavalForeignHullRegistry>(out var registry) && registry.Contains(ship);

    /// <summary>[Any thread] The copy running this physics script in the client container patches resolve from.</summary>
    internal static bool TryGetCopyHere(NavalPhysics physics, out SteeredCopy copy)
    {
        copy = null;
        return ContainerProvider.TryResolve<INavalForeignHullRegistry>(out var registry) && registry.TryGetCopy(physics, out copy);
    }

    /// <summary>[Any thread] Whether the entity's root is a copy's hull in the client container patches resolve from.</summary>
    internal static bool ContainsRootOfHere(WeakGameEntity entity) =>
        ContainerProvider.TryResolve<INavalForeignHullRegistry>(out var registry) && registry.ContainsRootOf(entity);

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
