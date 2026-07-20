using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IAgentPositionInterpolator
{
    /// <summary>Record the latest target frame the owner reported for a rider puppet.</summary>
    void SetRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection);

    /// <summary>Record the latest target frame the owner reported for a mounted rider puppet.</summary>
    void SetMountedRiderTarget(
        Agent agent,
        Vec3 targetPosition,
        Vec2 riderMovementDirection,
        Vec2 mountMovementDirection,
        Vec3 mountSnapPosition);

    /// <summary>Record the latest target frame the owner reported for a mount puppet (wider tolerances).</summary>
    void SetMountTarget(Agent mountAgent, Vec3 targetPosition, Vec2 movementDirection);

    /// <summary>Stop tracking an agent (e.g. it dismounted or was removed).</summary>
    void Forget(Agent agent);

    /// <summary>[Game thread] Apply each tracked agent's latest native target frame.</summary>
    void Tick(float dt);

    /// <summary>Drop all tracked targets (mission end).</summary>
    void Clear();
}

/// <summary>
/// [Game thread] Drives received puppets toward the position their owner last reported. On-foot puppets use the
/// engine's native target-frame path; mounted puppets are eased directly onto the owner's reported mount position
/// each frame, since a physically-seeked horse collides with the local crowd and can't reach the owner's spot.
/// Teleport handles large spawn/desync gaps.
/// <para>
/// All access is on the game thread — packet applies run inside <c>AgentMovementHandler</c>'s
/// <c>GameThread.RunSafe</c> and <see cref="Tick"/> runs in <c>OnMissionTick</c>, both serialized on the game
/// loop — so no locking is needed.
/// </para>
/// </summary>
public class AgentPositionInterpolator : IAgentPositionInterpolator
{
    // Snap only when the replicated owner is far enough away that local locomotion has clearly diverged.
    private const float RiderSnapDistance = 6f;
    private const float MountSnapDistance = 12f;
    private const float StaleTargetSeconds = 1f;
    // Exponential ease rate for the mounted-puppet position follow: fraction MountedFollowRate*dt of the gap is
    // closed each frame, so it tracks the owner with a small lag and settles when the owner stops.
    private const float MountedFollowRate = 12f;
    private const float MountedPositionEpsilon = 0.0001f;

    private readonly Dictionary<Agent, TargetFrame> _targets = new Dictionary<Agent, TargetFrame>();
    // Reused scratch list so eviction doesn't allocate every tick.
    private readonly List<Agent> _evict = new List<Agent>();
    private float elapsed;

    public void SetRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection)
    {
        if (agent == null) return;
        _targets[agent] = new TargetFrame(
            targetPosition,
            movementDirection,
            hasMountSnapPosition: false,
            Vec3.Zero,
            mountedRiderMovementDirection: Vec2.Zero,
            updatedAt: elapsed);
    }

    public void SetMountedRiderTarget(
        Agent agent,
        Vec3 targetPosition,
        Vec2 riderMovementDirection,
        Vec2 mountMovementDirection,
        Vec3 mountSnapPosition)
    {
        if (agent == null) return;
        _targets[agent] = new TargetFrame(
            targetPosition,
            mountMovementDirection,
            hasMountSnapPosition: true,
            mountSnapPosition,
            riderMovementDirection,
            elapsed);
    }

    public void SetMountTarget(Agent mountAgent, Vec3 targetPosition, Vec2 movementDirection)
    {
        if (mountAgent == null) return;
        _targets[mountAgent] = new TargetFrame(
            targetPosition,
            movementDirection,
            hasMountSnapPosition: false,
            Vec3.Zero,
            mountedRiderMovementDirection: Vec2.Zero,
            updatedAt: elapsed);
    }

    public void Forget(Agent agent)
    {
        if (agent == null) return;

        _targets.Remove(agent);
    }

    public void Clear() => _targets.Clear();

    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        elapsed += dt;
        if (_targets.Count == 0) return;

        foreach (var pair in _targets)
        {
            Agent agent = pair.Key;
            // Evict agents whose native object is gone (mission teardown, death). IsActive() mirrors the guard on
            // every other native-agent touch (see the movement-capture teardown races).
            if (!agent.IsActive())
            {
                _evict.Add(agent);
                continue;
            }

            // A stale target (owner stopped reporting) expires instead of pinning the puppet to an old position.
            if (elapsed - pair.Value.UpdatedAt > StaleTargetSeconds)
            {
                _evict.Add(agent);
                continue;
            }

            // Mounted riders are eased onto their horse's reported position directly, so they don't use snapDistance.
            if (agent.MountAgent != null)
            {
                FollowMounted(agent, pair.Value, dt);
                continue;
            }

            // A mount tolerates more slack before we snap; an on-foot rider is held tighter.
            float snapDistance = agent.IsMount ? MountSnapDistance : RiderSnapDistance;
            if (agent.Position.Distance(pair.Value.Position) <= snapDistance)
                MoveTowardTarget(agent, pair.Value);
            else
                Teleport(agent, pair.Value);
        }

        if (_evict.Count > 0)
        {
            foreach (Agent agent in _evict)
                _targets.Remove(agent);
            _evict.Clear();
        }
    }

    private static void MoveTowardTarget(Agent agent, TargetFrame target)
    {
        Vec2 targetPosition = target.Position.AsVec2;
        Vec3 targetDirection = ResolveDirection(agent, target);
        agent.SetTargetPositionAndDirection(in targetPosition, in targetDirection);
    }

    // Position a mounted puppet by easing its HORSE directly toward the owner's reported mount position each frame.
    // Unlike SetTargetPositionAndDirection this does NOT physically seek/collide, so the puppet tracks the owner's
    // exact position instead of getting wedged in the local crowd, and with no seek running the synced gait action
    // isn't clobbered. TeleportToPosition on the mount carries its rider along.
    private static void FollowMounted(Agent rider, TargetFrame target, float dt)
    {
        Agent mount = rider.MountAgent;
        if (mount == null || !mount.IsActive()) return;

        Vec3 mountTarget = target.HasMountSnapPosition ? target.MountSnapPosition : target.Position;
        Vec3 cur = mount.Position;
        float distance = cur.Distance(mountTarget);
        if (distance <= MountedPositionEpsilon)
        {
            return;
        }

        float alpha = System.Math.Min(1f, MountedFollowRate * dt);
        Vec3 next = distance > MountSnapDistance ? mountTarget : cur + ((mountTarget - cur) * alpha);

        mount.TeleportToPosition(next);
        mount.SetMovementDirection(target.MovementDirection);
        // Teleporting a horse rewrites its rider's movement direction from the rider look direction. Put the
        // owner's direction back in the same frame so the puppet does not flip on every correction.
        rider.SetMovementDirection(target.MountedRiderMovementDirection);
    }

    private static Vec3 ResolveDirection(Agent agent, TargetFrame target)
    {
        Vec2 direction = target.MovementDirection;
        if (direction.LengthSquared <= 0.0001f)
            direction = target.Position.AsVec2 - agent.Position.AsVec2;
        if (direction.LengthSquared <= 0.0001f)
            direction = agent.LookDirection.AsVec2;
        if (direction.LengthSquared <= 0.0001f)
            direction = Vec2.Forward;

        direction.Normalize();
        return new Vec3(direction.X, direction.Y, 0f);
    }

    private static void Teleport(Agent agent, TargetFrame target)
    {
        var lookDirection = agent.LookDirection;
        var movementDirection = agent.GetMovementDirection();
        if (agent.MountAgent != null && target.HasMountSnapPosition)
        {
            Teleport(agent.MountAgent, new TargetFrame(
                target.MountSnapPosition,
                target.MovementDirection,
                hasMountSnapPosition: false,
                Vec3.Zero,
                target.MountedRiderMovementDirection,
                target.UpdatedAt));
        }
        else
        {
            agent.TeleportToPosition(target.Position);
        }

        agent.LookDirection = lookDirection;
        agent.SetMovementDirection(movementDirection);
        MoveTowardTarget(agent, target);
    }

    private struct TargetFrame
    {
        public TargetFrame(
            Vec3 position,
            Vec2 movementDirection,
            bool hasMountSnapPosition,
            Vec3 mountSnapPosition,
            Vec2 mountedRiderMovementDirection,
            float updatedAt)
        {
            Position = position;
            MovementDirection = movementDirection;
            HasMountSnapPosition = hasMountSnapPosition;
            MountSnapPosition = mountSnapPosition;
            MountedRiderMovementDirection = mountedRiderMovementDirection;
            UpdatedAt = updatedAt;
        }

        public Vec3 Position { get; }
        public Vec2 MovementDirection { get; }
        public bool HasMountSnapPosition { get; }
        public Vec3 MountSnapPosition { get; }
        public Vec2 MountedRiderMovementDirection { get; }
        public float UpdatedAt { get; }
    }
}
