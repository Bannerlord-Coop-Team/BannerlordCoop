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
/// [Game thread] DEBUG: measures each copy's drift from its owner's latest sample, the closest approach of every hull pair,
/// the frame cost and the steering cost.
/// </summary>
public class NavalCopyProbeLogic : MissionLogic
{
    private readonly INetworkShipRegistry registry;
    private readonly INavalSteeredCopies steeredCopies;
    private readonly Dictionary<MissionShip, HullOutline> outlines = new Dictionary<MissionShip, HullOutline>();
    private readonly RollingWindow frameMilliseconds = new RollingWindow();
    private readonly RollingWindow steerMilliseconds = new RollingWindow();
    private long fixedTicks;

    public NavalCopyProbeLogic(INetworkShipRegistry registry, INavalSteeredCopies steeredCopies)
    {
        this.registry = registry;
        this.steeredCopies = steeredCopies;
    }

    /// <summary>The steered copies whose knobs <c>copy_steer</c> tunes.</summary>
    internal INavalSteeredCopies SteeredCopies => steeredCopies;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        float time = Mission.CurrentTime;
        frameMilliseconds.Add(time, dt * 1000f);
        steerMilliseconds.Add(time, steeredCopies.LastTickMilliseconds);
        MeasureDrift(time);
        MeasureApproaches(time);
    }

    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        fixedTicks++;
    }

    // Against the owner's latest raw sample, which a steered copy is pushed toward and a written one is set to.
    private void MeasureDrift(float time)
    {
        foreach (var entry in steeredCopies.Copies)
        {
            var ship = entry.Key;
            var steering = entry.Value.Steering;
            if (!steering.HasTarget || ship.IsRemoved || !ship.GameEntity.IsValid) continue;

            var frame = ship.GlobalFrame;
            var target = steering.SampleFrame;
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
            knobs = steeredCopies.Knobs.ToString(),
            frameCost = new
            {
                meanMs = CopyDiagnostics.Round(frameMilliseconds.Mean),
                maxMs = CopyDiagnostics.Round(frameMilliseconds.Max),
                steerMeanMs = CopyDiagnostics.Round(steerMilliseconds.Mean),
                steerMaxMs = CopyDiagnostics.Round(steerMilliseconds.Max),
                simulatedHulls = hulls?.Count(ship => ship.GameEntity.IsValid && ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation()),
                steeredCopies = steeredCopies.Copies.Count(entry => !entry.Value.WritesFrames),
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
                        mode = CopyDiagnostics.ModeName(hull),
                        writesFrames = copy.WritesFrames,
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
