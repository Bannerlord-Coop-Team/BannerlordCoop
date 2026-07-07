using Common.Logging;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IAgentPositionInterpolator
{
    /// <summary>Record the latest target frame the owner reported for a rider puppet.</summary>
    void SetRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection);

    /// <summary>Record the latest target frame the owner reported for a mounted rider puppet.</summary>
    void SetMountedRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection, Vec3 mountSnapPosition);

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
/// [Game thread] Drives received puppets toward the position their owner last reported using the engine's native
/// target-frame path. This avoids sliding a body with TeleportToPosition while keeping teleport as an emergency
/// correction for spawn/real desync.
/// <para>
/// All access is on the game thread — packet applies run inside <c>AgentMovementHandler</c>'s
/// <c>GameThread.RunSafe</c> and <see cref="Tick"/> runs in <c>OnMissionTick</c>, both serialized on the game
/// loop — so no locking is needed.
/// </para>
/// </summary>
public class AgentPositionInterpolator : IAgentPositionInterpolator
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentPositionInterpolator>();

    // Snap only when the replicated owner is far enough away that local locomotion has clearly diverged.
    private const float RiderSnapDistance = 6f;
    private const float MountSnapDistance = 12f;
    private const float DiagnosticInterval = 2f;
    private const float StaleTargetSeconds = 1f;
    private const float MountedDiagInterval = 0.5f;

    private readonly Dictionary<Agent, TargetFrame> _targets = new Dictionary<Agent, TargetFrame>();
    // Reused scratch list so eviction doesn't allocate every tick.
    private readonly List<Agent> _evict = new List<Agent>();
    // Temporary diagnostic: per-mounted-puppet drive telemetry, keyed by rider (removed on eviction). Used to
    // decide whether easing the position actually gives the horse real velocity or just teleport-drags it.
    private readonly Dictionary<Agent, MountedDiagSample> _mountedDiag = new Dictionary<Agent, MountedDiagSample>();
    private float diagnosticElapsed;
    private float elapsed;

    public void SetRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection)
    {
        if (agent == null) return;
        _targets[agent] = new TargetFrame(targetPosition, movementDirection, hasMountSnapPosition: false, Vec3.Zero, elapsed);
    }

    public void SetMountedRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection, Vec3 mountSnapPosition)
    {
        if (agent == null) return;
        _targets[agent] = new TargetFrame(targetPosition, movementDirection, hasMountSnapPosition: true, mountSnapPosition, elapsed);
    }

    public void SetMountTarget(Agent mountAgent, Vec3 targetPosition, Vec2 movementDirection)
    {
        if (mountAgent == null) return;
        _targets[mountAgent] = new TargetFrame(targetPosition, movementDirection, hasMountSnapPosition: false, Vec3.Zero, elapsed);
    }

    public void Forget(Agent agent)
    {
        if (agent == null) return;

        _targets.Remove(agent);
    }

    public void Clear()
    {
        _targets.Clear();
        _mountedDiag.Clear();
    }

    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        elapsed += dt;
        if (_targets.Count == 0) return;

        var foot = new DiagnosticBucket();
        var mounted = new DiagnosticBucket();
        var mounts = new DiagnosticBucket();

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

            // Tolerances are constant per kind, so derive them from the agent instead of storing them per target:
            // a mount/mounted rider tolerates more slack before we snap; an on-foot rider is held tighter.
            bool isMountedRider = agent.MountAgent != null;
            bool isMount = agent.IsMount;
            float snapDistance = isMount || isMountedRider ? MountSnapDistance : RiderSnapDistance;
            DiagnosticBucket bucket = isMount ? mounts : (isMountedRider ? mounted : foot);

            Vec3 target = pair.Value.Position;
            float dist = agent.Position.Distance(target);
            bucket.Track(dist);

            if (elapsed - pair.Value.UpdatedAt > StaleTargetSeconds)
            {
                bucket.Stale++;
                _evict.Add(agent);
                continue;
            }

            if (isMountedRider)
                LogMountedDiag(agent, dist, dist > snapDistance, dt);

            if (dist <= snapDistance)
            {
                MoveTowardTarget(agent, pair.Value);
                continue;
            }

            Teleport(agent, pair.Value);
            bucket.Snaps++;
        }

        if (_evict.Count > 0)
        {
            foreach (Agent agent in _evict)
            {
                _targets.Remove(agent);
                _mountedDiag.Remove(agent);
            }
            _evict.Clear();
        }

        diagnosticElapsed += dt;
        if (diagnosticElapsed >= DiagnosticInterval)
        {
            diagnosticElapsed = 0f;
            int tracked = foot.Tracked + mounted.Tracked + mounts.Tracked;
            if (tracked > 0)
            {
                Logger.Debug(
                    "[PuppetTargetDiag] foot={FootCount}/{FootSnaps}/{FootStale}/{FootMax:0.00} mounted={MountedCount}/{MountedSnaps}/{MountedStale}/{MountedMax:0.00} mounts={MountCount}/{MountSnaps}/{MountStale}/{MountMax:0.00}",
                    foot.Tracked,
                    foot.Snaps,
                    foot.Stale,
                    foot.MaxDistance,
                    mounted.Tracked,
                    mounted.Snaps,
                    mounted.Stale,
                    mounted.MaxDistance,
                    mounts.Tracked,
                    mounts.Snaps,
                    mounts.Stale,
                    mounts.MaxDistance);
            }
        }
    }

    private static void MoveTowardTarget(Agent agent, TargetFrame target)
    {
        Vec2 targetPosition = target.Position.AsVec2;
        Vec3 targetDirection = ResolveDirection(agent, target);
        agent.SetTargetPositionAndDirection(in targetPosition, in targetDirection);
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

    // Temporary diagnostic. For a mounted puppet, log the on-screen glide speed (frame-to-frame position delta)
    // next to the engine's real velocity on both rider and horse, plus the horse's gait action. If the horse
    // glides while its real velocity stays near zero, the gait has no drive and it slides, which means easing the
    // position isn't enough and the horse needs real velocity. Throttled per rider.
    private void LogMountedDiag(Agent rider, float distToTarget, bool snapped, float dt)
    {
        Agent mount = rider.MountAgent;
        if (mount == null || !mount.IsActive() || dt <= 0f) return;

        Vec3 riderPos = rider.Position;
        Vec3 mountPos = mount.Position;

        bool hadPrev = _mountedDiag.TryGetValue(rider, out var prev);
        float riderGlide = hadPrev ? riderPos.Distance(prev.RiderPos) / dt : 0f;
        float mountGlide = hadPrev ? mountPos.Distance(prev.MountPos) / dt : 0f;
        bool doLog = !hadPrev || elapsed - prev.LastLogElapsed >= MountedDiagInterval;

        _mountedDiag[rider] = new MountedDiagSample(riderPos, mountPos, doLog ? elapsed : prev.LastLogElapsed);
        if (!doLog) return;

        Logger.Debug(
            "[MountedDrive] rider={Rider} mount={Mount} dist={Dist:0.00} snap={Snap} glide(r/m)={RiderGlide:0.00}/{MountGlide:0.00} realVel(r/m)={RiderVel:0.00}/{MountVel:0.00} moveVel={MoveVel:0.00} input(r/m)={RiderInput:0.00}/{MountInput:0.00} gait={Gait}/{GaitType}",
            rider.Index,
            mount.Index,
            distToTarget,
            snapped,
            riderGlide,
            mountGlide,
            rider.GetRealGlobalVelocity().Length,
            mount.GetRealGlobalVelocity().Length,
            mount.MovementVelocity.Length,
            rider.MovementInputVector.Length,
            mount.MovementInputVector.Length,
            mount.GetCurrentAction(0).Index,
            mount.GetCurrentActionType(0));
    }

    private struct MountedDiagSample
    {
        public MountedDiagSample(Vec3 riderPos, Vec3 mountPos, float lastLogElapsed)
        {
            RiderPos = riderPos;
            MountPos = mountPos;
            LastLogElapsed = lastLogElapsed;
        }

        public Vec3 RiderPos { get; }
        public Vec3 MountPos { get; }
        public float LastLogElapsed { get; }
    }

    private struct TargetFrame
    {
        public TargetFrame(Vec3 position, Vec2 movementDirection, bool hasMountSnapPosition, Vec3 mountSnapPosition, float updatedAt)
        {
            Position = position;
            MovementDirection = movementDirection;
            HasMountSnapPosition = hasMountSnapPosition;
            MountSnapPosition = mountSnapPosition;
            UpdatedAt = updatedAt;
        }

        public Vec3 Position { get; }
        public Vec2 MovementDirection { get; }
        public bool HasMountSnapPosition { get; }
        public Vec3 MountSnapPosition { get; }
        public float UpdatedAt { get; }
    }

    private class DiagnosticBucket
    {
        public int Tracked { get; private set; }
        public int Snaps { get; set; }
        public int Stale { get; set; }
        public float MaxDistance { get; private set; }

        public void Track(float distance)
        {
            Tracked++;
            if (distance > MaxDistance)
                MaxDistance = distance;
        }
    }
}
