using Common.Logging;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using Serilog;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>[Game thread] This engine's copies: each keeps its dynamic body and is steered toward its owner's latest sample.</summary>
public interface INavalSteeredCopies
{
    /// <summary>The correction knobs every copy of this engine reads each step.</summary>
    CopySteeringKnobs Knobs { get; }

    /// <summary>Every tracked copy (diagnostics).</summary>
    IReadOnlyDictionary<MissionShip, SteeredCopy> Copies { get; }

    /// <summary>Starts steering a copy; one whose body is not simulating gets its owner's frames written instead.</summary>
    void Track(MissionShip ship);

    /// <summary>Takes the owner's latest sample of a copy.</summary>
    void Observe(MissionShip ship, MatrixFrame frame, BattleShipMotion motion);

    /// <summary>[Fixed physics step] Pushes every steered copy toward its target.</summary>
    void Tick(float fixedDt);

    /// <summary>Stops tracking a hull that left the mission.</summary>
    void Forget(MissionShip ship);

#if DEBUG
    /// <summary>DEBUG: wall time of the last <see cref="Tick"/> in milliseconds.</summary>
    float LastTickMilliseconds { get; }
#endif
}

/// <summary>One tracked copy: its steering, or that its owner's frames are written to it because its body is not simulating.</summary>
public sealed class SteeredCopy
{
    internal SteeredCopy(CopySteering steering, bool writesFrames)
    {
        Steering = steering;
        WritesFrames = writesFrames;
    }

    internal CopySteering Steering { get; }

    /// <summary>The fallback: the body was not simulating when the copy was tracked, so samples are written as frames.</summary>
    public bool WritesFrames { get; }

#if DEBUG
    internal CopyDiagnostics Diagnostics { get; } = new CopyDiagnostics();
#endif
}

/// <inheritdoc cref="INavalSteeredCopies"/>
public class NavalSteeredCopies : INavalSteeredCopies
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalSteeredCopies>();

    private readonly Dictionary<MissionShip, SteeredCopy> copies = new Dictionary<MissionShip, SteeredCopy>();
#if DEBUG
    private readonly Stopwatch tickClock = new Stopwatch();

    public float LastTickMilliseconds { get; private set; }
#endif

    public CopySteeringKnobs Knobs { get; } = new CopySteeringKnobs();

    public IReadOnlyDictionary<MissionShip, SteeredCopy> Copies => copies;

    public void Track(MissionShip ship)
    {
        if (copies.ContainsKey(ship)) return;

        bool simulating = IsSimulating(ship);
        copies.Add(ship, new SteeredCopy(new CopySteering(Knobs), writesFrames: !simulating));
        if (!simulating)
            Logger.Warning("[NavalSync] Copy hull {Index} has no simulating body; writing its owner's frames instead of steering it",
                ship.Index);
    }

    public void Observe(MissionShip ship, MatrixFrame frame, BattleShipMotion motion)
    {
        if (!ship.GameEntity.IsValid) return;
        if (!copies.ContainsKey(ship)) Track(ship);

        var copy = copies[ship];
        copy.Steering.ObserveOwner(frame, motion.Velocity, motion.YawRate);
        if (!copy.WritesFrames) return;

        ship.GameEntity.SetGlobalFrame(frame, isTeleportation: false);
        ship.GameEntity.UpdateAttachedNavigationMeshFaces();
    }

    public void Tick(float fixedDt)
    {
#if DEBUG
        tickClock.Restart();
#endif
        foreach (var entry in copies)
        {
            var ship = entry.Key;
            if (entry.Value.WritesFrames || ship.IsRemoved || !IsSimulating(ship)) continue;

            SteerOne(ship, entry.Value, fixedDt);
        }
#if DEBUG
        LastTickMilliseconds = (float)tickClock.Elapsed.TotalMilliseconds;
#endif
    }

    public void Forget(MissionShip ship) => copies.Remove(ship);

    // One correction step: a velocity and yaw rate change, or a teleport to the owner's frame that carries the crew.
    private static void SteerOne(MissionShip ship, SteeredCopy copy, float dt)
    {
        var entity = ship.GameEntity;
        var frame = ship.GlobalFrame;
        var velocity = entity.GetLinearVelocity();
        var angular = entity.GetAngularVelocity();
        var step = copy.Steering.Compute(dt, frame.origin, CopySteering.YawOf(frame.rotation.f), velocity.AsVec2, angular.z);
#if DEBUG
        copy.Diagnostics.RecordStep(Mission.Current?.CurrentTime ?? 0f, step);
#endif
        if (step.Snap)
        {
            ship.ShipsLogic.TeleportShip(ship, step.SnapFrame, checkFreeArea: false);
            var ownerVelocity = copy.Steering.FeedForwardVelocity;
            entity.SetLinearVelocity(new Vec3(ownerVelocity.x, ownerVelocity.y, 0f));
            Logger.Debug("[NavalSync] Snapped copy hull {Index} to its owner's frame ({Snaps} snaps)", ship.Index, copy.Steering.Snaps);
            return;
        }

        var deltaVelocity = step.DeltaVelocity;
        if (deltaVelocity.x != 0f || deltaVelocity.y != 0f)
            entity.SetLinearVelocity(new Vec3(velocity.x + deltaVelocity.x, velocity.y + deltaVelocity.y, velocity.z));
        if (step.DeltaYawRate != 0f) entity.SetAngularVelocity(new Vec3(angular.x, angular.y, angular.z + step.DeltaYawRate));
    }

    // Whether the hull's dynamic body simulates, so velocity changes move it.
    private static bool IsSimulating(MissionShip ship) =>
        ship.GameEntity.IsValid && ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation();
}
