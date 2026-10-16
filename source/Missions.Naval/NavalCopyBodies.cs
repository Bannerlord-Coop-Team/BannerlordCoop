#if DEBUG
using Common.Logging;
using NavalDLC.Missions.Objects;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: the copy_body modes of this mission's copies. Static because the ship engine's spawn and frame
/// write read it; <see cref="CoopNavalBattleBehavior"/> reverts a copy before vanilla removes it and clears it all at
/// mission end. Nothing here is ever sent: the owner stays the authority of every copy.
/// </summary>
internal static class NavalCopyBodies
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(NavalCopyBodies));

    private static readonly CopyBodyModeTable<MissionShip> table = new CopyBodyModeTable<MissionShip>(new MissionShipCopyBodyDriver());

    internal static CopyBodyModeTable<MissionShip> Table => table;

    /// <summary>Whether a copy of <paramref name="shipId"/> spawning now keeps its dynamic body to be steered.</summary>
    internal static bool SpawnsSteered(Guid shipId) => table.ModeFor(shipId) == CopyBodyMode.Steered;

    /// <summary>Tracks a copy the engine just spawned or released, in the mode its body is in.</summary>
    internal static void Track(MissionShip ship, Guid shipId)
    {
        var entry = table.Track(ship, shipId);
        Logger.Information("[NavalCopyBody] Copy {ShipId} is {Mode} (body simulating {Simulating})", shipId, entry.Mode,
            ship.GameEntity.IsValid && ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation());
    }

    /// <summary>
    /// Takes the owner's frame the replicator writes to a copy. True when the copy is steered, so the frame is a target
    /// for its velocity correction and must not be written.
    /// </summary>
    internal static bool ObserveOwnerFrame(MissionShip ship, in MatrixFrame frame)
    {
        if (!table.Entries.TryGetValue(ship, out var entry) || entry.Mode != CopyBodyMode.Steered) return false;

        entry.Steering.ObserveOwner(Mission.Current?.CurrentTime ?? 0f, frame.origin, CopySteering.YawOf(frame.rotation.f));
        return true;
    }

    /// <summary>[Physics step] Pushes every steered copy toward its owner's latest frame.</summary>
    internal static void Steer(float dt)
    {
        foreach (var entry in table.Entries)
        {
            if (entry.Value.Mode != CopyBodyMode.Steered) continue;

            var ship = entry.Key;
            if (ship.IsRemoved || !ship.GameEntity.IsValid || !ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation()) continue;

            SteerOne(ship, entry.Value.Steering, dt);
        }
    }

    private static void SteerOne(MissionShip ship, CopySteering steering, float dt)
    {
        var entity = ship.GameEntity;
        var frame = ship.GlobalFrame;
        var velocity = entity.GetLinearVelocity();
        var angular = entity.GetAngularVelocity();
        var step = steering.Compute(dt, frame.origin, CopySteering.YawOf(frame.rotation.f), velocity, angular.z);
        if (step.Snap)
        {
            var target = frame;
            target.origin = steering.TargetOrigin;
            entity.SetGlobalFrame(in target, isTeleportation: true);
            var ownerVelocity = steering.OwnerVelocity;
            entity.SetLinearVelocity(new Vec3(ownerVelocity.x, ownerVelocity.y, velocity.z));
            return;
        }

        if (step.DeltaVelocity.x != 0f || step.DeltaVelocity.y != 0f) entity.SetLinearVelocity(velocity + step.DeltaVelocity);
        if (step.DeltaYawRate != 0f) entity.SetAngularVelocity(new Vec3(angular.x, angular.y, angular.z + step.DeltaYawRate));
    }

    /// <summary>Reverts a copy vanilla is about to remove; an own hull is not tracked.</summary>
    internal static void OnRemoving(MissionShip ship) => table.OnRemoving(ship);

    internal static void Clear() => table.Clear();

    /// <summary>Rules and per-copy modes (diagnostics).</summary>
    internal static object Inspect() => new
    {
        rules = table.Rules.Select(rule => new
        {
            target = rule.Target,
            mode = rule.Mode.ToString(),
            knobs = rule.Mode == CopyBodyMode.Steered ? rule.Knobs.ToString() : null,
        }).ToArray(),
        copies = table.Entries.Select(entry => new
        {
            shipId = entry.Value.ShipId,
            mode = entry.Value.Mode.ToString(),
            requested = table.ModeFor(entry.Value.ShipId).ToString(),
            blocked = entry.Value.Blocked,
            bodySimulating = entry.Key.GameEntity.IsValid && entry.Key.GameEntity.HasDynamicRigidBodyAndActiveSimulation(),
            staticBody = entry.Key.GameEntity.IsValid && entry.Key.GameEntity.HasStaticPhysicsBody(),
            snaps = entry.Value.Steering?.Snaps,
        }).ToArray(),
    };

    /// <summary>
    /// The engine side of a mode switch. The static body is how vanilla gives a spawned item a moveable static collider
    /// (Mission.SpawnWeaponAsDropFromAgentAux): Moveable flag, then AddPhysics with isStatic.
    /// </summary>
    private sealed class MissionShipCopyBodyDriver : ICopyBodyDriver<MissionShip>
    {
        private const int NoCollisionGroup = -1;

        private readonly System.Collections.Generic.Dictionary<MissionShip, SavedBody> saved =
            new System.Collections.Generic.Dictionary<MissionShip, SavedBody>();

        public bool IsSimulating(MissionShip hull) =>
            hull.GameEntity.IsValid && hull.GameEntity.HasDynamicRigidBodyAndActiveSimulation();

        public void DisableSimulation(MissionShip hull)
        {
            if (hull.GameEntity.IsValid && hull.GameEntity.HasDynamicRigidBodyAndActiveSimulation())
                hull.GameEntity.DisableDynamicBodySimulation();
        }

        public string MakeStatic(MissionShip hull)
        {
            var entity = hull.GameEntity;
            if (!entity.IsValid) return "the copy's entity is gone";

            var shape = entity.GetBodyShape();
            if (shape == null) return "the copy's entity has no body shape";

            var body = new SavedBody(shape, entity.Mass, entity.CenterOfMass, entity.GetPhysicsMaterial(),
                entity.BodyFlag.HasAnyFlag(BodyFlags.Moveable));
            entity.RemovePhysics();
            if (!body.WasMoveable) entity.AddBodyFlags(BodyFlags.Moveable, applyToChildren: false);
            entity.AddPhysics(body.Mass, body.CenterOfMass, body.Shape, Vec3.Zero, Vec3.Zero, body.Material, isStatic: true,
                NoCollisionGroup);
            saved[hull] = body;
            return entity.HasStaticPhysicsBody() ? null : RevertFailedStatic(hull, "AddPhysics(isStatic) left no static body");
        }

        public void RestoreFromStatic(MissionShip hull)
        {
            if (!saved.TryGetValue(hull, out var body)) return;
            saved.Remove(hull);

            var entity = hull.GameEntity;
            if (!entity.IsValid) return;

            entity.RemovePhysics();
            if (!body.WasMoveable) entity.RemoveBodyFlags(BodyFlags.Moveable, applyToChildren: false);
            entity.AddPhysics(body.Mass, body.CenterOfMass, body.Shape, Vec3.Zero, Vec3.Zero, body.Material, isStatic: false,
                NoCollisionGroup);
            if (entity.HasDynamicRigidBodyAndActiveSimulation()) entity.DisableDynamicBodySimulation();
        }

        private string RevertFailedStatic(MissionShip hull, string reason)
        {
            RestoreFromStatic(hull);
            return reason;
        }

        private sealed class SavedBody
        {
            public SavedBody(PhysicsShape shape, float mass, Vec3 centerOfMass, PhysicsMaterial material, bool wasMoveable)
            {
                Shape = shape;
                Mass = mass;
                CenterOfMass = centerOfMass;
                Material = material;
                WasMoveable = wasMoveable;
            }

            public PhysicsShape Shape { get; }
            public float Mass { get; }
            public Vec3 CenterOfMass { get; }
            public PhysicsMaterial Material { get; }
            public bool WasMoveable { get; }
        }
    }
}
#endif
