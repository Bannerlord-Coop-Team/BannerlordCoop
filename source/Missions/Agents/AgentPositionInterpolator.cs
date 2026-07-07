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

    private readonly Dictionary<Agent, TargetFrame> _targets = new Dictionary<Agent, TargetFrame>();
    // Reused scratch list so eviction doesn't allocate every tick.
    private readonly List<Agent> _evict = new List<Agent>();
    private float diagnosticElapsed;

    public void SetRiderTarget(Agent agent, Vec3 targetPosition, Vec2 movementDirection)
    {
        if (agent == null) return;
        _targets[agent] = new TargetFrame(targetPosition, movementDirection);
    }

    public void SetMountTarget(Agent mountAgent, Vec3 targetPosition, Vec2 movementDirection)
    {
        if (mountAgent == null) return;
        _targets[mountAgent] = new TargetFrame(targetPosition, movementDirection);
    }

    public void Forget(Agent agent)
    {
        if (agent == null) return;

        _targets.Remove(agent);
    }

    public void Clear() => _targets.Clear();

    public void Tick(float dt)
    {
        if (_targets.Count == 0 || dt <= 0f) return;

        int tracked = 0;
        int targetFrames = 0;
        int snaps = 0;
        float maxDistance = 0f;

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

            // Skip mounted riders; their position is already driven by the mount's position.
            if (agent.MountAgent != null)
                continue;

            tracked++;

            // Tolerances are constant per kind, so derive them from the agent instead of storing them per target:
            // a mount tolerates more slack before we snap; a rider is held tighter.
            bool isMount = agent.IsMount;
            float snapDistance = isMount ? MountSnapDistance : RiderSnapDistance;

            Vec3 target = pair.Value.Position;
            float dist = agent.Position.Distance(target);
            if (dist > maxDistance)
                maxDistance = dist;

            if (dist <= snapDistance)
            {
                MoveTowardTarget(agent, pair.Value);
                targetFrames++;
                continue;
            }

            Teleport(agent, pair.Value);
            snaps++;
        }

        if (_evict.Count > 0)
        {
            foreach (Agent agent in _evict)
                _targets.Remove(agent);
            _evict.Clear();
        }

        diagnosticElapsed += dt;
        if (diagnosticElapsed >= DiagnosticInterval)
        {
            diagnosticElapsed = 0f;
            if (tracked > 0)
            {
                Logger.Debug(
                    "[PuppetTargetDiag] tracked={Tracked} targetFrames={TargetFrames} snaps={Snaps} maxDist={MaxDistance:0.00}",
                    tracked,
                    targetFrames,
                    snaps,
                    maxDistance);
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
        agent.TeleportToPosition(target.Position);
        agent.LookDirection = lookDirection;
        agent.SetMovementDirection(movementDirection);
        MoveTowardTarget(agent, target);
    }

    private struct TargetFrame
    {
        public TargetFrame(Vec3 position, Vec2 movementDirection)
        {
            Position = position;
            MovementDirection = movementDirection;
        }

        public Vec3 Position { get; }
        public Vec2 MovementDirection { get; }
    }
}
