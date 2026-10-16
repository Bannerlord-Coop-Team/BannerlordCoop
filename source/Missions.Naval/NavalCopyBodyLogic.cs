#if DEBUG
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: keeps every copy in the mode its copy_body rule asks for and steers the steered ones once per
/// physics step. Copies the engine did not spawn itself (a hull handed to another owner) are picked up from the registry.
/// Also measures each copy's drift from its owner's frames, the closest approach of every hull pair and the frame cost.
/// </summary>
public class NavalCopyBodyLogic : MissionLogic
{
    // Without a fixed step for this long the correction runs once per frame instead.
    private const float FixedTickTimeoutSeconds = 0.5f;

    private readonly INetworkShipRegistry registry;
    private readonly Dictionary<MissionShip, HullOutline> outlines = new Dictionary<MissionShip, HullOutline>();
    private readonly RollingWindow frameMilliseconds = new RollingWindow();
    private readonly RollingWindow steerMilliseconds = new RollingWindow();
    private readonly Stopwatch steerClock = new Stopwatch();
    private float sinceFixedTick = float.MaxValue;
    private long fixedTicks;

    public NavalCopyBodyLogic(INetworkShipRegistry registry)
    {
        this.registry = registry;
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        float time = Mission.CurrentTime;
        frameMilliseconds.Add(time, dt * 1000f);
        TrackRegisteredCopies();
        NavalCopyBodies.Table.ReconcileAll();

        sinceFixedTick += dt;
        if (sinceFixedTick > FixedTickTimeoutSeconds) TimedSteer(dt, time);

        MeasureDrift(time);
        MeasureApproaches(time);
    }

    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        fixedTicks++;
        sinceFixedTick = 0f;
        TimedSteer(fixedDt, Mission.CurrentTime);
    }

    private void TimedSteer(float dt, float time)
    {
        steerClock.Restart();
        NavalCopyBodies.Steer(dt);
        steerMilliseconds.Add(time, (float)steerClock.Elapsed.TotalMilliseconds);
    }

    private void TrackRegisteredCopies()
    {
        foreach (var ship in registry.Ships)
        {
            if (!(ship.Hull is MissionShip hull) || !NavalForeignHulls.Contains(hull) || NavalCopyBodies.Table.IsTracked(hull)) continue;

            NavalCopyBodies.Track(hull, ship.ShipId);
        }
    }

    // Against the owner's latest frame, which a kinematic or static copy is written to and a steered one is pushed toward.
    private static void MeasureDrift(float time)
    {
        foreach (var entry in NavalCopyBodies.Table.Entries)
        {
            var ship = entry.Key;
            var owner = entry.Value.OwnerFrame;
            if (owner == null || ship.IsRemoved || !ship.GameEntity.IsValid) continue;

            var frame = ship.GlobalFrame;
            var target = owner.Value;
            var diagnostics = entry.Value.Diagnostics;
            diagnostics.Distance.Add(time, (frame.origin.AsVec2 - target.origin.AsVec2).Length);
            diagnostics.HeadingDegrees.Add(time, Math.Abs(CopySteering.WrapAngle(
                CopySteering.YawOf(target.rotation.f) - CopySteering.YawOf(frame.rotation.f))) / CopySteering.DegToRad);
            diagnostics.Heave.Add(time, Math.Abs(frame.origin.z - target.origin.z));
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
                NavalContactProbe.RecordApproach(hulls[first].ShipId, hulls[second].ShipId, measured.CenterDistance, measured.Gap,
                    time, ModeOf(hulls[first].Hull) + "/" + ModeOf(hulls[second].Hull));
            }
        }
    }

    private static string ModeOf(MissionShip hull)
    {
        if (!NavalForeignHulls.Contains(hull)) return "own";
        return NavalCopyBodies.Table.Entries.TryGetValue(hull, out var entry) ? entry.Mode.ToString().ToLowerInvariant() : "kinematic";
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

    /// <summary>Modes, drift, correction, contact and frame cost on this client (diagnostics).</summary>
    internal object Inspect()
    {
        var hulls = Mission.GetMissionBehavior<NavalDLC.Missions.MissionLogics.NavalShipsLogic>()?.AllShips;
        return new
        {
            fixedTicks,
            frameCost = new
            {
                meanMs = CopyDiagnostics.Round(frameMilliseconds.Mean),
                maxMs = CopyDiagnostics.Round(frameMilliseconds.Max),
                steerMeanMs = CopyDiagnostics.Round(steerMilliseconds.Mean),
                steerMaxMs = CopyDiagnostics.Round(steerMilliseconds.Max),
                simulatedHulls = hulls?.Count(ship => ship.GameEntity.IsValid && ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation()),
                steeredCopies = NavalCopyBodies.Table.Entries.Count(entry => entry.Value.Mode == CopyBodyMode.Steered),
                hulls = hulls?.Count,
            },
            copyBodies = NavalCopyBodies.Inspect(),
            contact = NavalContactProbe.Inspect(),
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
