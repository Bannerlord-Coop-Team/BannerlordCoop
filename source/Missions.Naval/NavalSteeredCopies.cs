using Common.Logging;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using Serilog;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
#if DEBUG
    /// <summary>DEBUG: every tracked copy.</summary>
    IReadOnlyDictionary<MissionShip, SteeredCopy> Copies { get; }
#endif

    /// <summary>Makes a hull another owner's copy here, for this engine and the patches, and leaves it without a controller.</summary>
    void Track(MissionShip ship);

    /// <summary>Takes the owner's latest sample of a tracked copy; a copy whose body isn't simulating gets the frame written.</summary>
    void Observe(MissionShip ship, MatrixFrame frame, BattleShipMotion motion);

    /// <summary>[Fixed physics step] Pushes every simulating copy toward its target; a snap waits for <see cref="ApplySnaps"/>.</summary>
    void Tick(float fixedDt);

    /// <summary>[Mission tick] Teleports the copies whose steering queued a snap since the last call.</summary>
    void ApplySnaps();

    /// <summary>Stops treating a hull as a copy, when it left the mission or this client took it over.</summary>
    void Forget(MissionShip ship);

#if DEBUG
    /// <summary>DEBUG: wall time of the last <see cref="Tick"/> in milliseconds.</summary>
    float LastTickMilliseconds { get; }
#endif
}

/// <inheritdoc cref="INavalSteeredCopies"/>
public class NavalSteeredCopies : INavalSteeredCopies
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalSteeredCopies>();

    private readonly CopySteeringKnobs knobs;
    // Copy on write: the fixed physics step enumerates it while a capture or a host change adds or drops a copy.
    private volatile Dictionary<MissionShip, SteeredCopy> copies = new Dictionary<MissionShip, SteeredCopy>();
#if DEBUG
    private readonly Stopwatch tickClock = new Stopwatch();

    /// <inheritdoc/>
    public float LastTickMilliseconds { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<MissionShip, SteeredCopy> Copies => copies;
#endif

    /// <summary>Copies steered with this client's shared knobs, which copy_steer tunes.</summary>
    public NavalSteeredCopies(CopySteeringKnobs knobs)
    {
        this.knobs = knobs;
    }

    /// <inheritdoc/>
    public void Track(MissionShip ship)
    {
        if (copies.ContainsKey(ship)) return;

        var copy = new SteeredCopy(new CopySteering(knobs));
        copies = new Dictionary<MissionShip, SteeredCopy>(copies) { [ship] = copy };
        NavalForeignHulls.Add(ship, copy);
        // The owner decides this hull. ForeignHullControllerPatch keeps every later write at no controller while it is a copy.
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
    }

    /// <inheritdoc/>
    public void Observe(MissionShip ship, MatrixFrame frame, BattleShipMotion motion)
    {
        if (!copies.TryGetValue(ship, out var copy) || ship.IsRemoved || !ship.GameEntity.IsValid) return;

        copy.CrewLoad = KeptLoad(copy.CrewLoad, motion.CrewLoad);
#if DEBUG
        var targetBefore = copy.Steering.HasTarget ? copy.Steering.Target.Position : (Vec2?)null;
#endif
        copy.Steering.ObserveOwner(frame, motion.Velocity, motion.YawRate);
#if DEBUG
        float jump = targetBefore.HasValue ? (copy.Steering.Target.Position - targetBefore.Value).Length : 0f;
        copy.Diagnostics.TargetJump.Add(Mission.Current?.CurrentTime ?? 0f, jump);
#endif
        if (IsSimulating(ship)) return;

        WarnWritten(ship, copy);
        ship.GameEntity.SetGlobalFrame(frame, isTeleportation: false);
        ship.GameEntity.UpdateAttachedNavigationMeshFaces();
    }

    /// <summary>The load a copy keeps: a sample without one keeps the last, so a frame that committed no weight doesn't drop it to vanilla.</summary>
    internal static BattleShipCrewLoad KeptLoad(BattleShipCrewLoad last, BattleShipCrewLoad sampled) => sampled ?? last;

    /// <inheritdoc/>
    public void Tick(float fixedDt)
    {
#if DEBUG
        tickClock.Restart();
#endif
        foreach (var entry in copies)
        {
            var ship = entry.Key;
            if (entry.Value.Steering.IsSnapQueued || ship.IsRemoved || !IsSimulating(ship)) continue;

            SteerOne(ship, entry.Value, fixedDt);
        }
#if DEBUG
        LastTickMilliseconds = (float)tickClock.Elapsed.TotalMilliseconds;
#endif
    }

    /// <inheritdoc/>
    public void ApplySnaps()
    {
        foreach (var entry in copies)
        {
            var copy = entry.Value;
            if (!copy.Steering.IsSnapQueued) continue;

            var target = copy.Steering.TakeSnapFrame();
            if (entry.Key.IsRemoved || !entry.Key.GameEntity.IsValid) continue;

            Snap(entry.Key, copy, target);
#if DEBUG
            copy.Diagnostics.RecordSnap();
#endif
        }
    }

    /// <inheritdoc/>
    public void Forget(MissionShip ship)
    {
        if (!copies.ContainsKey(ship)) return;

        var remaining = new Dictionary<MissionShip, SteeredCopy>(copies);
        remaining.Remove(ship);
        copies = remaining;
        NavalForeignHulls.Remove(ship);
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

    // One correction step from the body's physics pose, as vanilla's fixed-tick code reads it, or a snap queued for the mission tick.
    private void SteerOne(MissionShip ship, SteeredCopy copy, float dt)
    {
        var entity = ship.GameEntity;
        var frame = entity.GetBodyWorldTransform();
        var velocity = entity.GetLinearVelocity();
        var angular = entity.GetAngularVelocity();
        var step = copy.Steering.Compute(dt, frame.origin, CopySteering.YawOf(frame.rotation.f), velocity.AsVec2, angular.z);
#if DEBUG
        copy.Diagnostics.RecordStep(Mission.Current?.CurrentTime ?? 0f, dt, knobs.TauSeconds, step);
#endif
        if (step.Snap) return;

        var deltaVelocity = step.DeltaVelocity;
        if (deltaVelocity.x != 0f || deltaVelocity.y != 0f)
            entity.SetLinearVelocity(new Vec3(velocity.x + deltaVelocity.x, velocity.y + deltaVelocity.y, velocity.z));
        if (step.DeltaYawRate != 0f) entity.SetAngularVelocity(new Vec3(angular.x, angular.y, angular.z + step.DeltaYawRate));
    }

    // Teleports the copy with its crew (vanilla) and anyone else standing on it, released from what they use, then gives it the
    // owner's motion.
    private static void Snap(MissionShip ship, SteeredCopy copy, MatrixFrame target)
    {
        var entity = ship.GameEntity;
        var before = entity.GetGlobalFrame();
        var riders = RidersOf(ship);
        ship.ShipsLogic.TeleportShip(ship, target, checkFreeArea: false);

        var after = entity.GetGlobalFrame();
        foreach (var rider in riders)
        {
            // A rider still bound to a machine or plank would be left holding a point that moved away from it.
            if (rider.CurrentlyUsedGameObject != null) rider.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
            rider.TeleportToPosition(after.TransformToParent(before.TransformToLocalNonOrthogonal(rider.Position)));
        }

        var ownerVelocity = copy.Steering.FeedForwardVelocity;
        entity.SetLinearVelocity(new Vec3(ownerVelocity.x, ownerVelocity.y, 0f));
        entity.SetAngularVelocity(new Vec3(0f, 0f, copy.Steering.FeedForwardYawRate));
        Logger.Debug("[NavalSync] Snapped copy hull {Index} to its owner's frame ({Riders} rider(s) moved along)", ship.Index, riders.Count);
    }

    // Agents standing on the copy that aren't its crew, like a local main agent that boarded it; TeleportShip moves only the crew.
    private static List<Agent> RidersOf(MissionShip ship)
    {
        var mission = Mission.Current;
        var crew = mission.GetMissionBehavior<NavalAgentsLogic>()?.GetActiveAgentsOfShip(ship);
        return mission.Agents
            .Where(agent => agent.IsActive() && agent.GetComponent<AgentNavalComponent>()?.SteppedShip == ship
                && (crew == null || !crew.Contains(agent)))
            .ToList();
    }
}

/// <summary>One tracked copy's steering toward its owner's samples, and the owner's crew load it heels under.</summary>
public sealed class SteeredCopy
{
    /// <summary>A copy driven by its own steering state.</summary>
    internal SteeredCopy(CopySteering steering)
    {
        Steering = steering;
    }

    /// <summary>The control law that steers this copy toward its owner's samples.</summary>
    internal CopySteering Steering { get; }

    /// <summary>Whether the warning that its body isn't simulating was logged.</summary>
    internal bool WarnedWritten { get; set; }

    private volatile BattleShipCrewLoad crewLoad;

    /// <summary>
    /// [Game thread writes, fixed tick reads] The owner's crew load from its latest sample that carried one; null before
    /// the first, which leaves the copy on vanilla's own crew weight.
    /// </summary>
    internal BattleShipCrewLoad CrewLoad
    {
        get => crewLoad;
        set => crewLoad = value;
    }

#if DEBUG
    /// <summary>DEBUG: this copy's drift, correction, snap and oscillation record.</summary>
    internal CopyDiagnostics Diagnostics { get; } = new CopyDiagnostics();
#endif
}
