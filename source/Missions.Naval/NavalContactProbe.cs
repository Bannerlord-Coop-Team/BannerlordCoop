#if DEBUG
using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// DEBUG: hull contact callbacks against copies, how collision and ram damage is routed, and every hull pair's closest
/// approach. Counters are interlocked because contact callbacks run on physics threads. Cleared at mission end.
/// </summary>
internal static class NavalContactProbe
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(NavalContactProbe));

    private static long contactOwnOnCopy, contactCopyOnOwn, contactCopyOnCopy, copyPairsFiltered;
    private static long collisionVanilla, collisionSent, collisionDropped, ramVanilla, ramSent, ramDropped;
    private static long routedCollisionApplies, ramPuppetsSpared;
    private static readonly Queue<Guid> sparedAgentIds = new Queue<Guid>();
    private static readonly Queue<Guid> receivedDeathAgentIds = new Queue<Guid>();
    private static readonly Queue<Guid> deathBroadcastAgentIds = new Queue<Guid>();
    private static readonly object agentIdsLock = new object();

    private static readonly Dictionary<(Guid, Guid), HullPairApproach> approaches = new Dictionary<(Guid, Guid), HullPairApproach>();

    /// <summary>[Physics thread] A hull contact callback of <paramref name="ship"/> against another hull, counted when a copy takes part.</summary>
    internal static void CountContact(MissionShip ship, WeakGameEntity otherEntity)
    {
        if (!CoopNavalMissionScope.IsActive || !otherEntity.IsValid) return;
        if (!(otherEntity.GetFirstScriptWithNameHash(MissionShip.MissionShipScriptNameHash) is MissionShip other)) return;

        bool shipIsCopy = NavalForeignHulls.Contains(ship);
        bool otherIsCopy = NavalForeignHulls.Contains(other);
        if (shipIsCopy && otherIsCopy) Interlocked.Increment(ref contactCopyOnCopy);
        else if (shipIsCopy) Interlocked.Increment(ref contactCopyOnOwn);
        else if (otherIsCopy) Interlocked.Increment(ref contactOwnOnCopy);
    }

    /// <summary>[Physics thread] The contact filter refused a pair of copies.</summary>
    internal static void CountFilteredCopyPair() => Interlocked.Increment(ref copyPairsFiltered);

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

    /// <summary>[Game thread] A ram impact here left another owner's puppet alive for its owner to kill.</summary>
    internal static void CountSparedRamPuppet(Agent puppet)
    {
        Interlocked.Increment(ref ramPuppetsSpared);
        var controller = Mission.Current?.GetMissionBehavior<CoopBattleController>();
        if (controller != null && controller.MissionComponent.AgentRegistry.TryGetAgentInfo(puppet, out var info))
            RecordAgentId(sparedAgentIds, info.AgentId);
        Logger.Debug("[NavalSync] Ram impact spared puppet agent {Agent}; its owner kills it", puppet.Index);
    }

    /// <summary>Records an owner's actual outgoing death message, using the same network id as the spared puppet.</summary>
    internal static void RecordDeathBroadcast(Guid agentId) => RecordAgentId(deathBroadcastAgentIds, agentId);

    /// <summary>Records an incoming owner death broadcast independently of its deferred puppet apply.</summary>
    internal static void RecordReceivedDeath(Guid agentId) => RecordAgentId(receivedDeathAgentIds, agentId);

    /// <summary>Keeps at most 128 recent ids per stream; repeats preserve repeated ram ticks.</summary>
    private static void RecordAgentId(Queue<Guid> ids, Guid agentId)
    {
        lock (agentIdsLock)
        {
            if (ids.Count == 128) ids.Dequeue();
            ids.Enqueue(agentId);
        }
    }

    /// <summary>Copies an id stream while the send callback may be writing it.</summary>
    private static Guid[] RecentAgentIds(Queue<Guid> ids)
    {
        lock (agentIdsLock) return ids.ToArray();
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
            ? NavalSteeredCopies.ModeName(firstHull) + "/" + NavalSteeredCopies.ModeName(secondHull)
            : NavalSteeredCopies.ModeName(secondHull) + "/" + NavalSteeredCopies.ModeName(firstHull);
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

    /// <summary>Counters, closest approaches and whether each registered hull carries a ram here (diagnostics).</summary>
    internal static object Inspect(IEnumerable<(Guid ShipId, MissionShip Hull)> hulls) => new
    {
        rams = hulls.Select(hull => new
        {
            ship = Short(hull.ShipId),
            copy = NavalForeignHulls.Contains(hull.Hull),
            hasRam = hull.Hull._ram != null,
        }).ToArray(),
        contactCallbacks = new
        {
            ownOnCopy = Interlocked.Read(ref contactOwnOnCopy),
            copyOnOwn = Interlocked.Read(ref contactCopyOnOwn),
            copyOnCopy = Interlocked.Read(ref contactCopyOnCopy),
            copyPairsFiltered = Interlocked.Read(ref copyPairsFiltered),
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
            ramPuppetsSpared = Interlocked.Read(ref ramPuppetsSpared),
            ramPuppetsSparedAgentIds = RecentAgentIds(sparedAgentIds),
            deathBroadcastAgentIds = RecentAgentIds(deathBroadcastAgentIds),
            receivedDeathAgentIds = RecentAgentIds(receivedDeathAgentIds),
        },
        closestApproach = InspectApproaches(),
    };

    internal static void Clear()
    {
        Interlocked.Exchange(ref contactOwnOnCopy, 0);
        Interlocked.Exchange(ref contactCopyOnOwn, 0);
        Interlocked.Exchange(ref contactCopyOnCopy, 0);
        Interlocked.Exchange(ref copyPairsFiltered, 0);
        Interlocked.Exchange(ref collisionVanilla, 0);
        Interlocked.Exchange(ref collisionSent, 0);
        Interlocked.Exchange(ref collisionDropped, 0);
        Interlocked.Exchange(ref ramVanilla, 0);
        Interlocked.Exchange(ref ramSent, 0);
        Interlocked.Exchange(ref ramDropped, 0);
        Interlocked.Exchange(ref routedCollisionApplies, 0);
        Interlocked.Exchange(ref ramPuppetsSpared, 0);
        lock (agentIdsLock)
        {
            sparedAgentIds.Clear();
            deathBroadcastAgentIds.Clear();
            receivedDeathAgentIds.Clear();
        }
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
