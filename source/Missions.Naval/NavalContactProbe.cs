#if DEBUG
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Missions.Naval;

/// <summary>
/// DEBUG: whether hulls touch copies on this client: ram raycast hits, hull contact callbacks, how the collision damage they
/// cause is routed, and the closest approach of every hull pair. Counters are written from physics threads, so they are
/// interlocked; the approaches are written on the game thread. Cleared at mission end.
/// </summary>
internal static class NavalContactProbe
{
    private static long ramRayHitsOnCopies, ramRayDamagePointsOnCopies, ramRayHitsByCopies;
    private static long contactOwnOnCopy, contactCopyOnOwn, contactCopyOnCopy;
    private static long collisionVanilla, collisionSent, collisionDropped, ramVanilla, ramSent, ramDropped;
    private static long routedCollisionApplies;

    private static readonly Dictionary<(Guid, Guid), HullPairApproach> approaches = new Dictionary<(Guid, Guid), HullPairApproach>();

    /// <summary>[Physics thread] A ram's raycast this fixed step: the ship it hit and whether it found a damage point.</summary>
    internal static void CountRamCheck(MissionShip rammer, MissionShip target, bool hasPoint)
    {
        if (!CoopNavalMissionScope.IsActive || rammer == null || target == null) return;

        if (NavalForeignHulls.Contains(rammer))
        {
            Interlocked.Increment(ref ramRayHitsByCopies);
            return;
        }

        if (!NavalForeignHulls.Contains(target)) return;

        Interlocked.Increment(ref ramRayHitsOnCopies);
        if (hasPoint) Interlocked.Increment(ref ramRayDamagePointsOnCopies);
    }

    /// <summary>[Physics thread] A hull contact callback of <paramref name="ship"/> against <paramref name="other"/>.</summary>
    internal static void CountContact(MissionShip ship, MissionShip other)
    {
        bool shipIsCopy = NavalForeignHulls.Contains(ship);
        bool otherIsCopy = NavalForeignHulls.Contains(other);
        if (shipIsCopy && otherIsCopy) Interlocked.Increment(ref contactCopyOnCopy);
        else if (shipIsCopy) Interlocked.Increment(ref contactCopyOnOwn);
        else if (otherIsCopy) Interlocked.Increment(ref contactOwnOnCopy);
    }

    /// <summary>[Any thread] How the damage gate routed one DealCollisionDamage call.</summary>
    internal static void CountCollisionRoute(BattleShipDamageRoute route, bool isRamDamage)
    {
        switch (route)
        {
            case BattleShipDamageRoute.Vanilla:
                Interlocked.Increment(ref isRamDamage ? ref ramVanilla : ref collisionVanilla);
                return;
            case BattleShipDamageRoute.Send:
                Interlocked.Increment(ref isRamDamage ? ref ramSent : ref collisionSent);
                return;
            default:
                Interlocked.Increment(ref isRamDamage ? ref ramDropped : ref collisionDropped);
                return;
        }
    }

    /// <summary>[Game thread] The owner applied a peer's collision report through vanilla DealCollisionDamage.</summary>
    internal static void CountRoutedCollisionApply() => Interlocked.Increment(ref routedCollisionApplies);

    /// <summary>[Game thread] Keeps the closest approach seen for a hull pair.</summary>
    internal static void RecordApproach(Guid first, MissionShip firstHull, Guid second, MissionShip secondHull, float centerDistance,
        float gap, float time)
    {
        bool inOrder = first.CompareTo(second) <= 0;
        var key = inOrder ? (first, second) : (second, first);
        if (!approaches.TryGetValue(key, out var approach))
        {
            approach = new HullPairApproach();
            approaches.Add(key, approach);
        }

        // The modes are only named for a new closest approach, in the pair's order.
        if (!approach.Take(centerDistance, gap, time)) return;
        approach.Modes = inOrder
            ? CopyDiagnostics.ModeName(firstHull) + "/" + CopyDiagnostics.ModeName(secondHull)
            : CopyDiagnostics.ModeName(secondHull) + "/" + CopyDiagnostics.ModeName(firstHull);
    }

    /// <summary>[Game thread] The closest approach of every hull pair, closest first.</summary>
    internal static object[] InspectApproaches() => approaches
        .OrderBy(entry => entry.Value.MinGap)
        .Select(entry => (object)new
        {
            pair = Short(entry.Key.Item1) + "/" + Short(entry.Key.Item2),
            minGapM = CopyDiagnostics.Round(entry.Value.MinGap),
            minCenterM = CopyDiagnostics.Round(entry.Value.MinCenter),
            atMissionTime = CopyDiagnostics.Round(entry.Value.AtTime),
            modes = entry.Value.Modes,
        })
        .ToArray();

    /// <summary>The closest approach summary of the pair that came closest, for a log line; null without pairs.</summary>
    internal static string DescribeClosest()
    {
        if (approaches.Count == 0) return null;

        var closest = approaches.OrderBy(entry => entry.Value.MinGap).First();
        return $"{Short(closest.Key.Item1)}/{Short(closest.Key.Item2)} gap {closest.Value.MinGap:0.00} m, centres " +
            $"{closest.Value.MinCenter:0.00} m at {closest.Value.AtTime:0.0} s ({closest.Value.Modes})";
    }

    internal static object Inspect() => new
    {
        ramRaycast = new
        {
            hitsOnCopies = Interlocked.Read(ref ramRayHitsOnCopies),
            damagePointsOnCopies = Interlocked.Read(ref ramRayDamagePointsOnCopies),
            hitsByCopies = Interlocked.Read(ref ramRayHitsByCopies),
        },
        contactCallbacks = new
        {
            ownOnCopy = Interlocked.Read(ref contactOwnOnCopy),
            copyOnOwn = Interlocked.Read(ref contactCopyOnOwn),
            copyOnCopy = Interlocked.Read(ref contactCopyOnCopy),
        },
        collisionDamage = new
        {
            vanilla = Interlocked.Read(ref collisionVanilla),
            sent = Interlocked.Read(ref collisionSent),
            dropped = Interlocked.Read(ref collisionDropped),
            ramVanilla = Interlocked.Read(ref ramVanilla),
            ramSent = Interlocked.Read(ref ramSent),
            ramDropped = Interlocked.Read(ref ramDropped),
            routedApplies = Interlocked.Read(ref routedCollisionApplies),
        },
        closestApproach = InspectApproaches(),
    };

    internal static void Clear()
    {
        Interlocked.Exchange(ref ramRayHitsOnCopies, 0);
        Interlocked.Exchange(ref ramRayDamagePointsOnCopies, 0);
        Interlocked.Exchange(ref ramRayHitsByCopies, 0);
        Interlocked.Exchange(ref contactOwnOnCopy, 0);
        Interlocked.Exchange(ref contactCopyOnOwn, 0);
        Interlocked.Exchange(ref contactCopyOnCopy, 0);
        Interlocked.Exchange(ref collisionVanilla, 0);
        Interlocked.Exchange(ref collisionSent, 0);
        Interlocked.Exchange(ref collisionDropped, 0);
        Interlocked.Exchange(ref ramVanilla, 0);
        Interlocked.Exchange(ref ramSent, 0);
        Interlocked.Exchange(ref ramDropped, 0);
        Interlocked.Exchange(ref routedCollisionApplies, 0);
        approaches.Clear();
    }

    internal static string Short(Guid shipId) => shipId.ToString("N").Substring(0, 8);

    private sealed class HullPairApproach
    {
        public float MinGap { get; private set; } = float.MaxValue;
        public float MinCenter { get; private set; } = float.MaxValue;
        public float AtTime { get; private set; }
        public string Modes { get; set; }

        /// <summary>True when the gap is a new closest approach.</summary>
        public bool Take(float centerDistance, float gap, float time)
        {
            MinCenter = Math.Min(MinCenter, centerDistance);
            if (gap >= MinGap) return false;

            MinGap = gap;
            AtTime = time;
            return true;
        }
    }
}
#endif
