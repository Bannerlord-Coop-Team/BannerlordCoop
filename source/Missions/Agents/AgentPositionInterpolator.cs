using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IAgentPositionInterpolator
{
    /// <summary>Record the latest position the owner reported for a rider puppet.</summary>
    void SetRiderTarget(Agent agent, Vec3 targetPosition);

    /// <summary>Record the latest position the owner reported for a mount puppet (wider tolerances).</summary>
    void SetMountTarget(Agent mountAgent, Vec3 targetPosition);

    /// <summary>Stop tracking an agent (e.g. it dismounted or was removed).</summary>
    void Forget(Agent agent);

    /// <summary>[Game thread] Ease every tracked agent one frame's worth toward its target.</summary>
    void Tick(float dt);

    /// <summary>Drop all tracked targets (mission end).</summary>
    void Clear();
}

/// <summary>
/// [Game thread] Eases synced puppets toward the position their owner last reported, EVERY frame, instead of
/// only correcting when a movement packet applies. The owner's input (which drives the puppet's own walk +
/// animation) is still applied on packet receipt in <see cref="Packets.AgentData.Apply"/>; this only reconciles
/// residual positional drift on top — but per-frame, so the smoothing is decoupled from the (bursty, ~10ms)
/// packet cadence that made a per-packet correction look stepped.
/// <para>
/// A packet apply pushes the latest target here (<see cref="SetRiderTarget"/> / <see cref="SetMountTarget"/>);
/// <see cref="Tick"/>, driven from <c>CoopMissionController.OnMissionTick</c>, nudges each tracked agent toward
/// its target with frame-rate-independent exponential smoothing (fraction closed per frame = 1 - e^(-dt/tau)).
/// Below a small deadzone nothing is corrected (the input-driven walk handles it); beyond a large snap distance
/// it teleports (spawn / genuine desync). Targets self-evict when their agent goes inactive.
/// </para>
/// <para>
/// All access is on the game thread — packet applies run inside <c>AgentMovementHandler</c>'s
/// <c>GameThread.RunSafe</c> and <see cref="Tick"/> runs in <c>OnMissionTick</c>, both serialized on the game
/// loop — so no locking is needed.
/// </para>
/// </summary>
public class AgentPositionInterpolator : IAgentPositionInterpolator
{
    // Easing time constant: fraction of the remaining gap closed each frame = 1 - e^(-dt/tau). Smaller = snappier
    // (closer to the old per-packet snap), larger = smoother but lags further behind. ~0.1s reads smooth at 60fps.
    private const float SmoothingTau = 0.3f;

    // Rider: correct small residual drift, snap only a large gap. Tunable starting points.
    private const float RiderDeadzone = 0.5f;
    private const float RiderSnapDistance = 6f;

    // Mount tolerates more slack before correcting.
    private const float MountDeadzone = 1f;
    private const float MountSnapDistance = 12f;

    private readonly struct Target
    {
        public readonly Vec3 Position;
        public readonly float Deadzone;
        public readonly float SnapDistance;

        public Target(Vec3 position, float deadzone, float snapDistance)
        {
            Position = position;
            Deadzone = deadzone;
            SnapDistance = snapDistance;
        }
    }

    private readonly Dictionary<Agent, Target> _targets = new Dictionary<Agent, Target>();
    // Reused scratch list so eviction doesn't allocate every tick.
    private readonly List<Agent> _evict = new List<Agent>();

    public void SetRiderTarget(Agent agent, Vec3 targetPosition)
    {
        if (agent == null) return;
        _targets[agent] = new Target(targetPosition, RiderDeadzone, RiderSnapDistance);
    }

    public void SetMountTarget(Agent mountAgent, Vec3 targetPosition)
    {
        if (mountAgent == null) return;
        _targets[mountAgent] = new Target(targetPosition, MountDeadzone, MountSnapDistance);
    }

    public void Forget(Agent agent)
    {
        if (agent != null) _targets.Remove(agent);
    }

    public void Clear() => _targets.Clear();

    public void Tick(float dt)
    {
        if (_targets.Count == 0 || dt <= 0f) return;

        float alpha = 1f - (float)Math.Exp(-dt / SmoothingTau);

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

            // Skip mounted riders; their position is already driven by the mount's position, so we don't need to
            if (agent.MountAgent != null)
                continue;

            Target target = pair.Value;
            float dist = agent.Position.Distance(target.Position);
            if (dist <= target.Deadzone)
                continue; // input-driven walk handles small drift

            var lookDirection = agent.LookDirection;
            var movementDirection = agent.GetMovementDirection();

            Vec3 next = dist > target.SnapDistance
                ? target.Position                                        // large gap: snap
                : Vec3.Lerp(agent.Position, target.Position, alpha);     // ease
            agent.TeleportToPosition(next);
            agent.LookDirection = lookDirection;
            agent.SetMovementDirection(movementDirection);
        }

        if (_evict.Count > 0)
        {
            foreach (Agent agent in _evict)
                _targets.Remove(agent);
            _evict.Clear();
        }
    }
}
