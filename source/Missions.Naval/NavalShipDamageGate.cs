using Common.Logging;
using Common.Messaging;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Decides inside the vanilla hull damage methods whether this client applies, sends or drops a hit, by
/// <see cref="BattleShipDamageRouter.Route"/>. Static because the Harmony patches read it; the scopes are per thread
/// because hull contact callbacks run on physics threads.
/// </summary>
internal static class NavalShipDamageGate
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(NavalShipDamageGate));

    [ThreadStatic] private static int routedApplyDepth;
    [ThreadStatic] private static HitScope hit;
    [ThreadStatic] private static int queuedContactDepth;
    [ThreadStatic] private static MissionShip contactShip;

    private static long suppressedHits, droppedContactCollisions;
#if DEBUG
    private static readonly ConcurrentDictionary<(int, int), int> contactLogTimes = new ConcurrentDictionary<(int, int), int>();
#endif

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

    internal static RoutedApplyScope RoutedApply()
    {
        routedApplyDepth++;
        return default;
    }

    /// <summary>Starts an attack on a hull; a puppet's attack is a replay its owner decides. Returns the outer scope.</summary>
    internal static HitScope EnterHit(Agent attacker, Vec3 worldPoint)
    {
        var outer = hit;
        var origin = BattleShipDamageRouter.AttackOrigin(attacker != null, attacker?.Controller == AgentControllerType.None);
        hit = new HitScope(origin, attacker, worldPoint);
        return outer;
    }

    internal static void ExitHit(HitScope outer) => hit = outer;

    internal static void EnterQueuedContact() => queuedContactDepth++;

    internal static void ExitQueuedContact() => queuedContactDepth--;

    /// <summary>Starts a hull's contact callback, whose hull is the rammer of any impact it queues. Returns the outer one.</summary>
    internal static MissionShip EnterContact(MissionShip ship)
    {
        var outer = contactShip;
        contactShip = ship;
        return outer;
    }

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

    /// <summary>Whether DealCollisionDamage runs vanilla on <paramref name="ship"/>.</summary>
    internal static bool AllowsCollision(MissionShip ship, MissionShip hitter, bool isRamDamage, Vec3 point, float damage)
    {
        var origin = BattleShipDamageRouter.CollisionOrigin(queuedContactDepth > 0, hitter == null || !NavalForeignHulls.Contains(hitter));
        return Allows(ship, origin, BattleShipDamageKind.Collision, damage, 0f, point, null, hitter, isRamDamage, -1);
    }

    /// <summary>Whether the running contact callback may queue an impact; only the rammer's owner keeps it.</summary>
    internal static bool KeepsContactCollision()
    {
        var rammer = contactShip;
        if (!CoopNavalMissionScope.IsActive || rammer == null) return true;
        if (BattleShipDamageRouter.KeepsContactCollision(!NavalForeignHulls.Contains(rammer))) return true;

        Interlocked.Increment(ref droppedContactCollisions);
        return false;
    }

    private static bool Allows(MissionShip ship, BattleShipHitOrigin origin, BattleShipDamageKind kind, float damage,
        float inflictedDamage, Vec3 worldPoint, Agent attacker, MissionShip hitter, bool isRamDamage, int sailIndex)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        var route = BattleShipDamageRouter.Route(!NavalForeignHulls.Contains(ship), routedApplyDepth > 0, origin);
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

#if DEBUG
    // Probe: whether hull contact callbacks fire against kinematic copies at all, at most once a second per hull pair.
    internal static void LogHullContact(MissionShip ship, WeakGameEntity other)
    {
        if (!CoopNavalMissionScope.IsActive || !other.IsValid) return;

        var otherShip = other.GetFirstScriptWithNameHash(MissionShip.MissionShipScriptNameHash) as MissionShip;
        if (otherShip == null || (!NavalForeignHulls.Contains(ship) && !NavalForeignHulls.Contains(otherShip))) return;

        var key = (ship.Index, otherShip.Index);
        int now = Environment.TickCount;
        if (contactLogTimes.TryGetValue(key, out int last) && now - last < 1000) return;
        contactLogTimes[key] = now;

        Logger.Debug("[NavalDamage] Hull contact callback on {Ship} (foreign {ShipForeign}, body active {ShipActive}) against " +
            "{Other} (foreign {OtherForeign}, body active {OtherActive})",
            Describe(ship), NavalForeignHulls.Contains(ship), ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation(),
            Describe(otherShip), NavalForeignHulls.Contains(otherShip), otherShip.GameEntity.HasDynamicRigidBodyAndActiveSimulation());
    }

    private static string Describe(MissionShip ship) =>
        "#" + ship.Index + " " + ship.ShipOrigin?.Hull?.StringId + " " + ship.Team?.TeamSide + "/" + ship.Formation?.FormationIndex;
#endif

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
#if DEBUG
        contactLogTimes.Clear();
#endif
    }
}
