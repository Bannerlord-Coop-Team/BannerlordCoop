using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
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
/// animation for engine-controlled agents) is still applied on packet receipt in <see cref="Packets.AgentData.Apply"/>;
/// humanoid and mount puppets use vanilla scripted movement toward the same target.
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

    // Puppets need a small deadzone so scripted movement, not teleport easing, carries visible locomotion.
    private const float RiderDeadzone = 0.15f;
    private const float RiderSnapDistance = 6f;

    // Mount tolerates more slack before correcting.
    private const float MountDeadzone = 1f;
    private const float MountSnapDistance = 12f;

    private const Agent.AIScriptedFrameFlags ScriptedPuppetMovementFlags =
        Agent.AIScriptedFrameFlags.GoToPosition
        | Agent.AIScriptedFrameFlags.ConsiderRotation
        | Agent.AIScriptedFrameFlags.NeverSlowDown
        | Agent.AIScriptedFrameFlags.NoAttack;

    private readonly Dictionary<Agent, Vec3> _targets = new Dictionary<Agent, Vec3>();
    // Reused scratch list so eviction doesn't allocate every tick.
    private readonly List<Agent> _evict = new List<Agent>();

    public void SetRiderTarget(Agent agent, Vec3 targetPosition)
    {
        if (agent == null) return;
        _targets[agent] = targetPosition;
    }

    public void SetMountTarget(Agent mountAgent, Vec3 targetPosition)
    {
        if (mountAgent == null) return;
        _targets[mountAgent] = targetPosition;
    }

    public void Forget(Agent agent)
    {
        if (agent == null) return;

        DisableScriptedMovement(agent);
        _targets.Remove(agent);
    }

    public void Clear()
    {
        foreach (Agent agent in _targets.Keys)
            DisableScriptedMovement(agent);

        _targets.Clear();
    }

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

            // Tolerances are constant per kind, so derive them from the agent instead of storing them per target:
            // a mount tolerates more slack before we correct/snap; a rider is held tighter.
            bool isMount = agent.IsMount;
            float deadzone = isMount ? MountDeadzone : RiderDeadzone;
            float snapDistance = isMount ? MountSnapDistance : RiderSnapDistance;

            Vec3 target = pair.Value;
            float dist = agent.Position.Distance(target);
            bool useScriptedMovement = agent.IsHuman || agent.IsMount;
            if (dist <= deadzone)
            {
                if (useScriptedMovement)
                    DisableScriptedMovement(agent);
                continue; // ignore tiny drift
            }

            if (useScriptedMovement)
            {
                if (dist > snapDistance)
                {
                    Teleport(agent, target);
                    DisableScriptedMovement(agent);
                }
                else
                {
                    MovePuppet(agent, target);
                }

                continue;
            }

            Vec3 next = dist > snapDistance
                ? target                                            // large gap: snap
                : Vec3.Lerp(agent.Position, target, alpha);         // ease
            Teleport(agent, next);
        }

        if (_evict.Count > 0)
        {
            foreach (Agent agent in _evict)
                _targets.Remove(agent);
            _evict.Clear();
        }
    }

    private static void MovePuppet(Agent agent, Vec3 target)
    {
        var scriptedPosition = new WorldPosition(agent.Mission.Scene, UIntPtr.Zero, target, hasValidZ: false);
        Vec2 direction = target.AsVec2 - agent.Position.AsVec2;
        if (direction.LengthSquared <= 0.0001f)
            direction = agent.GetMovementDirection();
        if (direction.LengthSquared <= 0.0001f)
            direction = agent.LookDirection.AsVec2;
        if (direction.LengthSquared <= 0.0001f)
            direction = Vec2.Forward;

        direction.Normalize();
        float scriptedDirection = (float)Math.Atan2(direction.Y, direction.X);
        agent.SetScriptedPositionAndDirection(
            ref scriptedPosition,
            scriptedDirection,
            addHumanLikeDelay: false,
            ScriptedPuppetMovementFlags);
    }

    private static void Teleport(Agent agent, Vec3 position)
    {
        var lookDirection = agent.LookDirection;
        var movementDirection = agent.GetMovementDirection();
        agent.TeleportToPosition(position);
        agent.LookDirection = lookDirection;
        agent.SetMovementDirection(movementDirection);
    }

    private static void DisableScriptedMovement(Agent agent)
    {
        if (agent == null || !agent.IsActive()) return;
        if (!agent.IsHuman && !agent.IsMount) return;

        agent.DisableScriptedMovement();
    }
}
