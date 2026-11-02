using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Places each settled oar and helm seat at vanilla's spawn placement, so an owner's agent and every copy of it sit at the
/// same spot relative to the station however the seat was reached or a stagger moved it.
/// </summary>
public interface IStationSeatSettler
{
    /// <summary>[Mission tick] Places a held seat's agent once each time its sit-down or stagger finishes while it keeps the seat.</summary>
    void Settle(Guid agentId, Agent agent, UsableMissionObject point, MissionObject hull);

    /// <summary>[Mission tick] Forgets the seats no <see cref="Settle"/> call visited since the last pass.</summary>
    void EndPass();

    /// <summary>[Game thread] The settle counters (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IStationSeatSettler"/>
public class StationSeatSettler : IStationSeatSettler
{
    // A settled agent this close to its placement is left where it is.
    internal const float PlacementTolerance = 0.01f;

    private readonly INavalShipEngine engine;
    private readonly Dictionary<Guid, SettleState> states = new Dictionary<Guid, SettleState>();
    private readonly List<Guid> scratchAgentIds = new List<Guid>();
    private int pass;
    private long placed, alreadyPlaced, detachedSkips;

    // Reads the seats and moves the agents through the engine.
    public StationSeatSettler(INavalShipEngine engine)
    {
        this.engine = engine;
    }

    /// <summary>A seat has settled once its pilot finished sitting down and plays no stagger.</summary>
    internal static bool IsSettled(in StationSeatPose pose) => pose.Kind != StationSeatKind.None && pose.IsSitting && !pose.IsStruck;

    /// <summary>
    /// Where vanilla's spawn seats the pilot (<c>ShipOarMachine</c>/<c>ShipControllerMachine.OnPilotAssignedDuringSpawn</c>): an
    /// oar's rower at the end of its sit-down from the raw station frame, the helm's pilot at the raw station origin.
    /// </summary>
    internal static Vec3 SpawnPlacement(in StationSeatPose pose)
    {
        if (pose.Kind != StationSeatKind.Oar) return pose.StationFrame.origin;

        var frame = pose.StationFrame;
        frame.rotation.Orthonormalize();
        var displacement = pose.SitDownDisplacement;
        return frame.TransformToParent(in displacement);
    }

    public void Settle(Guid agentId, Agent agent, UsableMissionObject point, MissionObject hull)
    {
        if (!states.TryGetValue(agentId, out var state))
        {
            state = new SettleState();
            states[agentId] = state;
        }

        state.Pass = pass;
        if (!IsHeld(agent, point, hull))
        {
            state.Point = null;
            return;
        }

        // A placed seat only needs watching for a stagger, which the agent alone tells without looking up its machine.
        if (ReferenceEquals(state.Point, point))
        {
            if (engine.IsStruck(agent)) state.Point = null;
            return;
        }

        var pose = engine.ReadSeatPose(agent, point);
        if (!IsSettled(pose)) return;

        state.Point = point;
        Place(agent, point, hull, pose);
    }

    // A seat is only placed while its agent holds it on a hull that is afloat and in the mission.
    private bool IsHeld(Agent agent, UsableMissionObject point, MissionObject hull) =>
        point != null && hull != null && engine.IsSeated(agent, point) && !engine.IsSinking(hull) && !engine.IsHullRemoved(hull);

    // Moves a settled agent to its spawn placement, unless its station left the hull or it already sits there.
    private void Place(Agent agent, UsableMissionObject point, MissionObject hull, in StationSeatPose pose)
    {
        if (!engine.TryDescribeStation(point, out var stationHull, out _, out _) || !ReferenceEquals(stationHull, hull))
        {
            detachedSkips++;
            return;
        }

        var target = SpawnPlacement(pose);
        if (pose.Position.DistanceSquared(target) <= PlacementTolerance * PlacementTolerance)
        {
            alreadyPlaced++;
            return;
        }

        engine.PlaceSeatedAgent(agent, target);
        placed++;
    }

    public void EndPass()
    {
        scratchAgentIds.Clear();
        foreach (var entry in states)
            if (entry.Value.Pass != pass) scratchAgentIds.Add(entry.Key);
        foreach (var agentId in scratchAgentIds)
            states.Remove(agentId);

        pass++;
    }

    public object Inspect() => new
    {
        placed,
        alreadyPlaced,
        detachedSkips,
        watched = states.Count,
    };

    // The point a seat was last placed at (null until it settles again) and the pass that last visited it.
    private sealed class SettleState
    {
        public UsableMissionObject Point { get; set; }
        public int Pass { get; set; }
    }
}
