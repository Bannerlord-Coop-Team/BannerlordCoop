#if DEBUG
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: measures each copy's drift from its owner's estimated pose, the closest approach of every hull pair,
/// the frame cost and the steering cost.
/// </summary>
public class NavalCopyProbeLogic : MissionLogic
{
    private readonly INetworkShipRegistry registry;
    private readonly INavalSteeredCopies steeredCopies;
    private readonly CopySteeringKnobs knobs;
    private readonly Dictionary<MissionShip, HullOutline> outlines = new Dictionary<MissionShip, HullOutline>();
    private readonly RollingWindow frameMilliseconds = new RollingWindow();
    private readonly RollingWindow steerMilliseconds = new RollingWindow();
    // Garbage collections per frame by generation, to tell a GC pause from steering in the steer cost's tail.
    private readonly RollingWindow[] gcCollections = { new RollingWindow(), new RollingWindow(), new RollingWindow() };
    private readonly int[] lastCollectionCounts = new int[3];
    private bool collectionCountsTaken;
    private long fixedTicks;

    public NavalCopyProbeLogic(INetworkShipRegistry registry, INavalSteeredCopies steeredCopies, CopySteeringKnobs knobs)
    {
        this.registry = registry;
        this.steeredCopies = steeredCopies;
        this.knobs = knobs;
    }

    /// <summary>The battle's steered copies, which the inspect summary reads the drift of.</summary>
    internal INavalSteeredCopies SteeredCopies => steeredCopies;

    /// <summary>The active or completed roll sampler, owned by this mission rather than a command instance.</summary>
    internal INavalRollProbe RollProbe { get; set; }

    /// <summary>Stops partial measurements before the mission and its hulls are released.</summary>
    public override void OnEndMissionInternal()
    {
        RollProbe?.Stop("mission_end");
        base.OnEndMissionInternal();
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        float time = Mission.CurrentTime;
        frameMilliseconds.Add(time, dt * 1000f);
        steerMilliseconds.Add(time, steeredCopies.LastTickMilliseconds);
        MeasureCollections(time);
        RollProbe?.Tick();
        MeasureDrift(time);
        MeasureApproaches(time);
    }

    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        fixedTicks++;
    }

    // Adds each generation's collections since the last frame; the first frame only takes the counts.
    private void MeasureCollections(float time)
    {
        for (int generation = 0; generation < gcCollections.Length; generation++)
        {
            int count = GC.CollectionCount(generation);
            if (collectionCountsTaken) gcCollections[generation].Add(time, count - lastCollectionCounts[generation]);
            lastCollectionCounts[generation] = count;
        }

        collectionCountsTaken = true;
    }

    // Against where the owner is now, its latest sample moved on by its age without the lead; heave against the sample's height.
    private void MeasureDrift(float time)
    {
        foreach (var entry in steeredCopies.Copies)
        {
            var ship = entry.Key;
            var steering = entry.Value.Steering;
            if (!steering.HasTarget || ship.IsRemoved || !ship.GameEntity.IsValid) continue;

            var frame = ship.GlobalFrame;
            var owner = steering.OwnerEstimate;
            var diagnostics = entry.Value.Diagnostics;
            diagnostics.Distance.Add(time, (frame.origin.AsVec2 - owner.Position).Length);
            diagnostics.HeadingDegrees.Add(time, Math.Abs(CopySteering.WrapAngle(owner.Yaw - CopySteering.YawOf(frame.rotation.f)))
                / CopySteering.DegToRad);
            diagnostics.Heave.Add(time, Math.Abs(frame.origin.z - steering.SampleFrame.origin.z));
        }
    }

    private void MeasureApproaches(float time)
    {
        var hulls = registry.Ships
            .Select(ship => (ship.ShipId, Hull: ship.Hull as MissionShip))
            .Where(ship => ship.Hull != null && !ship.Hull.IsRemoved && ship.Hull.GameEntity.IsValid)
            .ToList();

        for (int first = 0; first < hulls.Count; first++)
        {
            var a = Outline(hulls[first].Hull);
            for (int second = first + 1; second < hulls.Count; second++)
            {
                var b = Outline(hulls[second].Hull);
                var measured = MeasureApproach(a.Center, a.Forward, a.HalfLength, a.HalfBeam, b.Center, b.Forward, b.HalfLength,
                    b.HalfBeam);
                NavalContactProbe.RecordApproach(hulls[first].ShipId, hulls[first].Hull, hulls[second].ShipId, hulls[second].Hull,
                    measured.CenterDistance, measured.Gap, time);
            }
        }
    }

    // The local physics box is fixed per hull: x across the beam, y along the keel, as MissionShipRam reads it.
    private HullOutline Outline(MissionShip hull)
    {
        if (!outlines.TryGetValue(hull, out var local))
        {
            var box = GameEntityPhysicsExtensions.GetLocalPhysicsBoundingBox(hull.GameEntity, false);
            local = new HullOutline(box.center, new Vec2(0f, 1f), (box.max.y - box.min.y) / 2f, (box.max.x - box.min.x) / 2f);
            outlines[hull] = local;
        }

        var frame = hull.GlobalFrame;
        return new HullOutline(frame.TransformToParent(local.Point), frame.rotation.f.AsVec2, local.HalfLength, local.HalfBeam);
    }

    /// <summary>
    /// Centre distance and the gap between two hulls modelled as capsules as long as the hull: a keel segment that stops
    /// half a beam short of bow and stern, padded by the half-beam. The gap is negative when they overlap.
    /// </summary>
    internal static (float CenterDistance, float Gap) MeasureApproach(Vec2 centerA, Vec2 forwardA, float halfLengthA, float halfBeamA,
        Vec2 centerB, Vec2 forwardB, float halfLengthB, float halfBeamB)
    {
        var keelA = forwardA.Normalized() * Math.Max(0f, halfLengthA - halfBeamA);
        var keelB = forwardB.Normalized() * Math.Max(0f, halfLengthB - halfBeamB);
        float keels = SegmentDistance(centerA - keelA, centerA + keelA, centerB - keelB, centerB + keelB);
        return ((centerA - centerB).Length, keels - halfBeamA - halfBeamB);
    }

    internal static float SegmentDistance(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2)
    {
        if (SegmentsCross(p1, p2, q1, q2)) return 0f;

        return Math.Min(Math.Min(PointSegmentDistance(p1, q1, q2), PointSegmentDistance(p2, q1, q2)),
            Math.Min(PointSegmentDistance(q1, p1, p2), PointSegmentDistance(q2, p1, p2)));
    }

    private static float PointSegmentDistance(Vec2 point, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        float lengthSquared = ab.LengthSquared;
        if (lengthSquared <= 0f) return (point - a).Length;

        float t = Math.Max(0f, Math.Min(1f, Vec2.DotProduct(point - a, ab) / lengthSquared));
        return (point - (a + (ab * t))).Length;
    }

    private static bool SegmentsCross(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1);
        float d2 = Cross(q2 - q1, p2 - q1);
        float d3 = Cross(p2 - p1, q1 - p1);
        float d4 = Cross(p2 - p1, q2 - p1);
        return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
    }

    private static float Cross(Vec2 a, Vec2 b) => (a.x * b.y) - (a.y * b.x);

    /// <summary>Knobs, drift, correction, contact and frame cost on this client (diagnostics).</summary>
    internal object Inspect()
    {
        var hulls = Mission.GetMissionBehavior<NavalDLC.Missions.MissionLogics.NavalShipsLogic>()?.AllShips;
        return new
        {
            fixedTicks,
            knobs = knobs.ToString(),
            frameCost = new
            {
                meanMs = CopyDiagnostics.Round(frameMilliseconds.Mean),
                maxMs = CopyDiagnostics.Round(frameMilliseconds.Max),
                steerMeanMs = CopyDiagnostics.Round(steerMilliseconds.Mean),
                steerP99Ms = CopyDiagnostics.Round(steerMilliseconds.Percentile(99f)),
                steerMaxMs = CopyDiagnostics.Round(steerMilliseconds.Max),
                gcCollections10s = new
                {
                    gen0 = (int)gcCollections[0].Sum,
                    gen1 = (int)gcCollections[1].Sum,
                    gen2 = (int)gcCollections[2].Sum,
                },
                simulatedHulls = hulls?.Count(NavalSteeredCopies.IsSimulating),
                steeredCopies = steeredCopies.Copies.Keys.Count(NavalSteeredCopies.IsSimulating),
                hulls = hulls?.Count,
            },
            copies = registry.Ships
                .Where(ship => ship.Hull is MissionShip hull && steeredCopies.Copies.ContainsKey(hull))
                .Select(ship =>
                {
                    var hull = (MissionShip)ship.Hull;
                    var copy = steeredCopies.Copies[hull];
                    return new
                    {
                        shipId = ship.ShipId,
                        mode = NavalSteeredCopies.ModeName(hull),
                        diagnostics = copy.Diagnostics.Inspect(copy.Steering),
                    };
                })
                .ToArray(),
            contact = NavalContactProbe.Inspect(registry.Ships
                .Where(ship => ship.Hull is MissionShip hull && !hull.IsRemoved && hull.GameEntity.IsValid)
                .Select(ship => (ship.ShipId, (MissionShip)ship.Hull))),
        };
    }

    /// <summary>A hull's keel centre, heading and half extents; the centre is hull-local in the cache and world otherwise.</summary>
    private readonly struct HullOutline
    {
        public HullOutline(Vec3 point, Vec2 forward, float halfLength, float halfBeam)
        {
            Point = point;
            Forward = forward;
            HalfLength = halfLength;
            HalfBeam = halfBeam;
        }

        public Vec3 Point { get; }
        public Vec2 Center => Point.AsVec2;
        public Vec2 Forward { get; }
        public float HalfLength { get; }
        public float HalfBeam { get; }
    }
}
#endif
