using Missions.Agents.Packets;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IAgentPositionInterpolator
{
    /// <summary>Record the latest continuous frame the owner reported for a rider puppet.</summary>
    void SetRiderTarget(Agent agent, AgentData data);

    /// <summary>Record the latest continuous frame the owner reported for a mounted rider puppet.</summary>
    void SetMountedRiderTarget(Agent agent, AgentData data);

    /// <summary>Record the latest continuous frame the owner reported for a mount puppet.</summary>
    void SetMountTarget(Agent mountAgent, AgentMountData data);

    /// <summary>Stop tracking an agent (e.g. it dismounted or was removed).</summary>
    void Forget(Agent agent);

    /// <summary>Read the latest owner-reported locomotion flags retained for a puppet.</summary>
    bool TryGetTargetMovementFlags(
        Agent agent,
        out uint riderMovementFlags,
        out uint mountMovementFlags);

    /// <summary>[Game thread] Apply each tracked agent's latest native target frame.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Restore received continuous state after the native Agent tick.</summary>
    void ReplayContinuousStates();

    /// <summary>Drop all tracked targets (mission end).</summary>
    void Clear();
}

/// <summary>
/// [Game thread] Drives received puppets toward the position their owner last reported. On-foot puppets use the
/// engine's native target-frame path; mounted puppets are eased directly onto the owner's reported mount position
/// while visible guard presentations defer that correction. Teleport handles large spawn/desync gaps.
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

    public void SetRiderTarget(Agent agent, AgentData data)
    {
        if (agent == null) return;
        _targets[agent] = new TargetFrame(
            data.Position,
            new ContinuousState(
                data.MovementDirection,
                data.LookDirection,
                agent.MovementInputVector,
                data.MovementFlag),
            hasMountSnapPosition: false,
            Vec3.Zero,
            default,
            updatedAt: elapsed);
    }

    public void SetMountedRiderTarget(Agent agent, AgentData data)
    {
        if (agent == null || data.MountData == null) return;
        Agent mount = agent.MountAgent;
        _targets[agent] = new TargetFrame(
            data.Position,
            new ContinuousState(
                data.MountData.MountMovementDirection,
                data.MountData.MountLookDirection,
                mount?.MovementInputVector ??
                    data.MountData.MountInputVector,
                data.MountData.MountMovementFlag),
            hasMountSnapPosition: true,
            data.MountData.MountPosition,
            new ContinuousState(
                data.MovementDirection,
                data.LookDirection,
                agent.MovementInputVector,
                data.MovementFlag),
            elapsed);
    }

    public void SetMountTarget(Agent mountAgent, AgentMountData data)
    {
        if (mountAgent == null) return;
        _targets[mountAgent] = new TargetFrame(
            data.MountPosition,
            new ContinuousState(
                data.MountMovementDirection,
                data.MountLookDirection,
                mountAgent.MovementInputVector,
                data.MountMovementFlag),
            hasMountSnapPosition: false,
            Vec3.Zero,
            default,
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
        Agent mount = agent.MountAgent;
        _targets[agent] = new TargetFrame(
            targetPosition,
            ContinuousState.Capture(mount, mountMovementDirection),
            hasMountSnapPosition: true,
            mountSnapPosition,
            ContinuousState.Capture(agent, riderMovementDirection),
            elapsed);
    }

    public void Forget(Agent agent)
    {
        if (agent == null) return;

        _targets.Remove(agent);
    }

    public bool TryGetTargetMovementFlags(
        Agent agent,
        out uint riderMovementFlags,
        out uint mountMovementFlags)
    {
        riderMovementFlags = 0;
        mountMovementFlags = 0;
        if (agent == null || !_targets.TryGetValue(agent, out TargetFrame target))
            return false;

        if (target.HasMountSnapPosition)
        {
            riderMovementFlags = target.MountedRiderState.MovementFlags;
            mountMovementFlags = target.AgentState.MovementFlags;
        }
        else
        {
            riderMovementFlags = target.AgentState.MovementFlags;
        }
        return true;
    }

    public void Clear() => _targets.Clear();

    public void ReplayContinuousStates()
    {
        foreach (var pair in _targets)
        {
            Agent agent = pair.Key;
            if (!agent.IsActive() ||
                agent.Health <= 0f ||
                elapsed - pair.Value.UpdatedAt > StaleTargetSeconds)
            {
                continue;
            }

            TargetFrame target = pair.Value;
            if (agent.MountAgent != null && target.HasMountSnapPosition)
            {
                target.MountedRiderState.Apply(agent);
                target.AgentState.Apply(agent.MountAgent);
            }
            else
            {
                target.AgentState.Apply(agent);
            }
        }
    }

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
            if (!agent.IsActive() || agent.Health <= 0f)
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
            pair.Value.AgentState.Apply(agent);
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
        Vec3 targetDirection = ResolveDirection(
            agent,
            target.Position,
            target.AgentState.MovementDirection);
        agent.SetTargetPositionAndDirection(in targetPosition, in targetDirection);
    }

    // Ease the horse directly toward the owner's reported position without a physical seek. A guard presentation
    // defers this semantic teleport because TeleportToPosition also teleports the rider and resets its components.
    private static void FollowMounted(Agent rider, TargetFrame target, float dt)
    {
        Agent mount = rider.MountAgent;
        if (mount == null || !mount.IsActive()) return;

        target.MountedRiderState.Apply(rider);
        target.AgentState.Apply(mount);

        Vec3 mountTarget = target.HasMountSnapPosition ? target.MountSnapPosition : target.Position;
        Vec3 cur = mount.Position;
        float distance = cur.Distance(mountTarget);
        if (HasGuardPresentation(rider) && distance <= MountSnapDistance)
        {
            // Defer smoothing teleports that disturb the rider's mounted guard timeline.
            return;
        }
        if (distance <= MountedPositionEpsilon)
        {
            return;
        }

        float alpha = System.Math.Min(1f, MountedFollowRate * dt);
        Vec3 next = distance > MountSnapDistance ? mountTarget : cur + ((mountTarget - cur) * alpha);

        mount.TeleportToPosition(next);
        // Teleporting a horse rewrites its rider's movement basis. Put both owner snapshots back in the same
        // frame so the puppet does not flip between movement packets.
        target.AgentState.Apply(mount);
        target.MountedRiderState.Apply(rider);
    }

    private static bool HasGuardPresentation(Agent rider)
    {
        if (AgentActionData.GetDefendMovementFlags(rider.MovementFlags)
            != Agent.MovementControlFlag.None)
        {
            return true;
        }

        return AgentActionData.IsGuardPresentationAction(
                rider.GetCurrentActionType(0))
            || AgentActionData.IsGuardPresentationAction(
                rider.GetCurrentActionType(1));
    }

    private static Vec3 ResolveDirection(
        Agent agent,
        Vec3 targetPosition,
        Vec2 movementDirection)
    {
        Vec2 direction = movementDirection;
        if (direction.LengthSquared <= 0.0001f)
            direction = targetPosition.AsVec2 - agent.Position.AsVec2;
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
                target.AgentState,
                hasMountSnapPosition: false,
                Vec3.Zero,
                default,
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
            ContinuousState agentState,
            bool hasMountSnapPosition,
            Vec3 mountSnapPosition,
            ContinuousState mountedRiderState,
            float updatedAt)
        {
            Position = position;
            AgentState = agentState;
            HasMountSnapPosition = hasMountSnapPosition;
            MountSnapPosition = mountSnapPosition;
            MountedRiderState = mountedRiderState;
            UpdatedAt = updatedAt;
        }

        public Vec3 Position { get; }
        public ContinuousState AgentState { get; }
        public bool HasMountSnapPosition { get; }
        public Vec3 MountSnapPosition { get; }
        public ContinuousState MountedRiderState { get; }
        public float UpdatedAt { get; }
    }

    private readonly struct ContinuousState
    {
        public ContinuousState(
            Vec2 movementDirection,
            Vec3 lookDirection,
            Vec2 movementInput,
            uint movementFlags)
        {
            MovementDirection = movementDirection;
            LookDirection = lookDirection;
            MovementInput = movementInput;
            MovementFlags = movementFlags;
        }

        public Vec2 MovementDirection { get; }
        public Vec3 LookDirection { get; }
        public Vec2 MovementInput { get; }
        public uint MovementFlags { get; }

        public static ContinuousState Capture(
            Agent agent,
            Vec2 movementDirection)
        {
            if (agent == null)
            {
                return new ContinuousState(
                    movementDirection,
                    new Vec3(
                        movementDirection.X,
                        movementDirection.Y,
                        0f),
                    Vec2.Zero,
                    0);
            }

            return new ContinuousState(
                movementDirection,
                agent.LookDirection,
                agent.MovementInputVector,
                (uint)AgentData.GetLocomotionMovementFlags(
                    agent.MovementFlags));
        }

        public void Apply(Agent agent)
        {
            if (agent == null ||
                !agent.IsActive() ||
                agent.Health <= 0f)
            {
                return;
            }

            agent.SetMovementDirection(MovementDirection);
            agent.LookDirection = LookDirection;
            agent.MovementInputVector = MovementInput;
            // Native continuous-state setters can consume or rewrite the move mask. Install it last so the
            // upcoming Agent tick sees the owner's translation and turn inputs.
            AgentData.ApplyLocomotionMovementFlags(
                agent,
                (Agent.MovementControlFlag)MovementFlags);
        }
    }
}
