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

/// <summary>
/// [Game thread] This engine's copies of other owners' hulls, the only writer of <see cref="NavalForeignHulls"/>: each copy
/// is steered toward its owner's latest sample while its body simulates, and gets its owner's frames written otherwise.
/// </summary>
public interface INavalSteeredCopies
{
    /// <summary>The correction knobs every copy of this engine reads each step.</summary>
    CopySteeringKnobs Knobs { get; }

#if DEBUG
    /// <summary>DEBUG: every tracked copy.</summary>
    IReadOnlyDictionary<MissionShip, SteeredCopy> Copies { get; }
#endif

    /// <summary>Makes a hull another owner's copy here, for this engine and the patches.</summary>
    void Track(MissionShip ship);

    /// <summary>Takes the owner's latest sample of a tracked copy; a copy whose body isn't simulating gets the frame written.</summary>
    void Observe(MissionShip ship, MatrixFrame frame, BattleShipMotion motion);

    /// <summary>[Fixed physics step] Pushes every simulating copy toward its target.</summary>
    void Tick(float fixedDt);

    /// <summary>Stops treating a hull as a copy, when it left the mission or this client took it over.</summary>
    void Forget(MissionShip ship);

#if DEBUG
    /// <summary>DEBUG: wall time of the last <see cref="Tick"/> in milliseconds.</summary>
    float LastTickMilliseconds { get; }
#endif
}

/// <summary>One tracked copy's steering toward its owner's samples.</summary>
#if DEBUG
public
#else
internal
#endif
sealed class SteeredCopy
{
    internal SteeredCopy(CopySteering steering)
    {
        Steering = steering;
    }

    internal CopySteering Steering { get; }

    /// <summary>Whether the warning that its body isn't simulating was logged.</summary>
    internal bool WarnedWritten { get; set; }

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

    public IReadOnlyDictionary<MissionShip, SteeredCopy> Copies => copies;
#endif

    public CopySteeringKnobs Knobs { get; } = new CopySteeringKnobs();

    public void Track(MissionShip ship)
    {
        if (copies.ContainsKey(ship)) return;

        copies.Add(ship, new SteeredCopy(new CopySteering(Knobs)));
        NavalForeignHulls.Add(ship);
    }

    public void Observe(MissionShip ship, MatrixFrame frame, BattleShipMotion motion)
    {
        if (!copies.TryGetValue(ship, out var copy) || ship.IsRemoved || !ship.GameEntity.IsValid) return;

        copy.Steering.ObserveOwner(frame, motion.Velocity, motion.YawRate);
        if (IsSimulating(ship)) return;

        WarnWritten(ship, copy);
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
            if (ship.IsRemoved || !IsSimulating(ship)) continue;

            SteerOne(ship, entry.Value, fixedDt);
        }
#if DEBUG
        LastTickMilliseconds = (float)tickClock.Elapsed.TotalMilliseconds;
#endif
    }

    public void Forget(MissionShip ship)
    {
        if (copies.Remove(ship)) NavalForeignHulls.Remove(ship);
    }

    /// <summary>"own", "steered" or "written": a copy is steered while its body simulates, otherwise its owner's frames are written.</summary>
    internal static string ModeName(MissionShip hull)
    {
        if (!NavalForeignHulls.Contains(hull)) return "own";
        return IsSimulating(hull) ? "steered" : "written";
    }

    // Whether the hull's dynamic body simulates, so velocity changes move it.
    internal static bool IsSimulating(MissionShip ship) =>
        ship.GameEntity.IsValid && ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation();

    // Logs once per copy that its owner's frames are written to it instead of steering it.
    private static void WarnWritten(MissionShip ship, SteeredCopy copy)
    {
        if (copy.WarnedWritten) return;

        copy.WarnedWritten = true;
        Logger.Warning("[NavalSync] Copy hull {Index} has no simulating body; writing its owner's frames instead of steering it",
            ship.Index);
    }

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
}
