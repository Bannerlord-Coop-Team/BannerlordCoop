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
/// [Game thread] Emergency position reconciliation for received puppets. Normal movement is driven from the
/// replicated input in <see cref="PuppetMovementComponent"/>; this only snaps large gaps from spawn/real desync.
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

        _targets.Remove(agent);
    }

    public void Clear() => _targets.Clear();

    public void Tick(float dt)
    {
        if (_targets.Count == 0 || dt <= 0f) return;

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
            // a mount tolerates more slack before we snap; a rider is held tighter.
            bool isMount = agent.IsMount;
            float snapDistance = isMount ? MountSnapDistance : RiderSnapDistance;

            Vec3 target = pair.Value;
            float dist = agent.Position.Distance(target);
            if (dist <= snapDistance)
                continue;

            Teleport(agent, target);
        }

        if (_evict.Count > 0)
        {
            foreach (Agent agent in _evict)
                _targets.Remove(agent);
            _evict.Clear();
        }
    }

    private static void Teleport(Agent agent, Vec3 position)
    {
        var lookDirection = agent.LookDirection;
        var movementDirection = agent.GetMovementDirection();
        agent.TeleportToPosition(position);
        agent.LookDirection = lookDirection;
        agent.SetMovementDirection(movementDirection);
    }
}
