using Common.Messaging;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Decides inside the vanilla hull damage methods whether this client applies, sends or drops a hit. Static for the Harmony
/// patches, with per-thread scopes because hull contact callbacks run on physics threads.
/// </summary>
internal static class NavalShipDamageGate
{
    [ThreadStatic] private static int routedApplyDepth;
    [ThreadStatic] private static HitScope hit;
    [ThreadStatic] private static int queuedContactDepth;
    [ThreadStatic] private static MissionShip contactShip;
    [ThreadStatic] private static bool inRoutedRamApply;

    private static long suppressedHits, droppedContactCollisions;

    /// <summary>The attack a hull damage method runs under: who decided it, its attacker and its world position.</summary>
    internal readonly struct HitScope
    {
        public HitScope(BattleShipHitOrigin origin, Agent attacker, Vec3 worldPoint)
        {
            IsActive = true;
            Origin = origin;
            Attacker = attacker;
            WorldPoint = worldPoint;
        }

        public bool IsActive { get; }
        public BattleShipHitOrigin Origin { get; }
        public Agent Attacker { get; }
        public Vec3 WorldPoint { get; }
    }

    /// <summary>Applying a routed hit or an owner's condition: the damage methods run vanilla on any hull.</summary>
    internal readonly struct RoutedApplyScope : IDisposable
    {
        public void Dispose() => routedApplyDepth--;
    }

    // Opens a routed apply, closed by disposing the returned scope.
    internal static RoutedApplyScope RoutedApply()
    {
        routedApplyDepth++;
        return default;
    }

    /// <summary>Starts an attack on a hull; a puppet's attack is a replay its owner decides. Returns the outer scope.</summary>
    internal static HitScope EnterHit(Agent attacker, Vec3 worldPoint)
    {
        var outer = hit;
        var origin = BattleShipDamageRoutes.AttackOrigin(attacker != null, attacker?.Controller == AgentControllerType.None);
        hit = new HitScope(origin, attacker, worldPoint);
        return outer;
    }

    // Restores the attack scope that was open before EnterHit.
    internal static void ExitHit(HitScope outer) => hit = outer;

    // Opens the scope of a hull's queued contact impacts, which all came from this client's rammer.
    internal static void EnterQueuedContact() => queuedContactDepth++;

    // Closes the queued contact scope.
    internal static void ExitQueuedContact() => queuedContactDepth--;

    /// <summary>Starts a hull's contact callback, whose hull is the rammer of any impact it queues. Returns the outer one.</summary>
    internal static MissionShip EnterContact(MissionShip ship)
    {
        var outer = contactShip;
        contactShip = ship;
        return outer;
    }

    // Restores the contact callback hull that was open before EnterContact.
    internal static void ExitContact(MissionShip outer) => contactShip = outer;

    /// <summary>Whether a hit method (hull, sails, fire) runs vanilla on <paramref name="ship"/>.</summary>
    internal static bool AllowsHit(MissionShip ship, BattleShipDamageKind kind, float damage, float inflictedDamage, Agent attacker,
        int sailIndex)
    {
        var scope = hit;
        var origin = scope.IsActive ? scope.Origin : BattleShipHitOrigin.SelfTick;
        return Allows(ship, origin, kind, damage, inflictedDamage, scope.IsActive ? scope.WorldPoint : Vec3.Invalid,
            attacker ?? scope.Attacker, null, false, sailIndex);
    }

    /// <summary>
    /// Whether DealCollisionDamage runs vanilla on <paramref name="ship"/>. A ram it skips still gives this client's agents vanilla's
    /// ram morale, so every client that sees the impact applies it once to its own agents (puppets aren't AI-controlled).
    /// </summary>
    internal static bool AllowsCollision(MissionShip ship, MissionShip hitter, bool isRamDamage, Vec3 point, float damage)
    {
        var origin = BattleShipDamageRoutes.CollisionOrigin(queuedContactDepth > 0, hitter == null || !NavalForeignHulls.Contains(hitter));
        if (Allows(ship, origin, BattleShipDamageKind.Collision, damage, 0f, point, null, hitter, isRamDamage, -1)) return true;

        if (isRamDamage) ship._moraleInteractionLogic?.OnShipRammed(hitter, ship);
        return false;
    }

    /// <summary>Opens a collision method's scope, marking a routed ram apply. Returns the outer one.</summary>
    internal static bool EnterCollision(bool isRamDamage)
    {
        var outer = inRoutedRamApply;
        inRoutedRamApply = isRamDamage && routedApplyDepth > 0;
        return outer;
    }

    // Restores the collision scope that was open before EnterCollision.
    internal static void ExitCollision(bool outer) => inRoutedRamApply = outer;

    /// <summary>Whether vanilla's ram morale runs: never in a routed ram apply, since each client gave its own agents theirs.</summary>
    internal static bool AllowsRamMorale() => !inRoutedRamApply;

    /// <summary>Whether the running contact callback may queue an impact; only the rammer's owner keeps it.</summary>
    internal static bool KeepsContactCollision()
    {
        var rammer = contactShip;
        if (!CoopNavalMissionScope.IsActive || rammer == null) return true;
        if (!NavalForeignHulls.Contains(rammer)) return true;

        Interlocked.Increment(ref droppedContactCollisions);
        return false;
    }

    // Runs vanilla when the route says so; otherwise skips it and publishes a hit to send to the hull's authority.
    private static bool Allows(MissionShip ship, BattleShipHitOrigin origin, BattleShipDamageKind kind, float damage,
        float inflictedDamage, Vec3 worldPoint, Agent attacker, MissionShip hitter, bool isRamDamage, int sailIndex)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        var route = BattleShipDamageRoutes.Route(!NavalForeignHulls.Contains(ship), routedApplyDepth > 0, origin);
#if DEBUG
        if (kind == BattleShipDamageKind.Collision) NavalContactProbe.CountCollisionRoute(route, isRamDamage);
#endif
        if (route == BattleShipDamageRoute.Vanilla) return true;

        Interlocked.Increment(ref suppressedHits);
        if (route == BattleShipDamageRoute.Send)
        {
            var localPoint = worldPoint.IsValid ? ship.GlobalFrame.TransformToLocal(worldPoint) : Vec3.Zero;
            MessageBroker.Instance.Publish(ship, new BattleShipHit(ship, kind, damage, inflictedDamage, localPoint, attacker, hitter,
                isRamDamage, sailIndex));
        }

        return false;
    }

#if DEBUG
    /// <summary>[Game thread] Deals hull damage the way a ship siege hit does, so the gate applies or routes it like one.</summary>
    internal static void DealLocalHit(MissionShip ship, float damage)
    {
        var outer = EnterHit(null, ship.GlobalFrame.origin);
        try
        {
            ship.DealDamage(damage, null, out _, out _, out _, out _);
        }
        finally
        {
            ExitHit(outer);
        }
    }
#endif

    /// <summary>The sail's index in the hull's sails, or -1.</summary>
    internal static int SailIndexOf(MissionShip ship, MissionSail sail)
    {
        if (sail == null) return -1;
        var sails = ship.Sails;
        for (int index = 0; index < sails.Count; index++)
        {
            if (sails[index] == sail) return index;
        }

        return -1;
    }

    /// <summary>[Game thread] Suppressed hits and dropped contact impacts on this client (diagnostics).</summary>
    internal static object Inspect() => new
    {
        suppressedHits = Interlocked.Read(ref suppressedHits),
        droppedContactCollisions = Interlocked.Read(ref droppedContactCollisions),
    };

    /// <summary>[Game thread] Resets the counters at mission end.</summary>
    internal static void Clear()
    {
        Interlocked.Exchange(ref suppressedHits, 0);
        Interlocked.Exchange(ref droppedContactCollisions, 0);
    }
}
