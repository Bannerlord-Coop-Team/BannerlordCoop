using Missions.Messages;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The NavalDLC hull operations the ship replicators need. Implemented in Missions.Naval, which is only loaded
/// while NavalDLC is active, so Missions itself binds to no DLC type.
/// </summary>
public interface INavalShipEngine
{
    /// <summary>The live hulls of the current mission.</summary>
    IReadOnlyList<MissionObject> Hulls { get; }

    /// <summary>The hull's campaign ship description, side, formation and frame under the given identity.</summary>
    BattleShipSpawnData Describe(MissionObject hull, NetworkShipInfo identity);

    /// <summary>Spawns a kinematic copy of another owner's hull on <paramref name="team"/>; null when it cannot.</summary>
    MissionObject SpawnForeignHull(BattleShipSpawnData data, Team team, out Formation formation);

    MatrixFrame GetFrame(MissionObject hull);

    /// <summary>Moves a foreign hull's kinematic body and its attached navmesh.</summary>
    void ApplyForeignFrame(MissionObject hull, MatrixFrame frame);

    /// <summary>The hull's effective helm input this frame.</summary>
    BattleShipInput ReadInput(MissionObject hull);

    /// <summary>Drives a copied hull's oars and sails with its owner's helm input.</summary>
    void ApplyInput(MissionObject hull, BattleShipInput input);

    /// <summary>Whether the hull's rigid body is simulating (diagnostics).</summary>
    bool IsBodyActive(MissionObject hull);

    /// <summary>The hull's ship controller type name (diagnostics).</summary>
    string ControllerName(MissionObject hull);

    /// <summary>The hull an on-foot agent stands on: its stepped ship, or a connected plank's source hull; null off ships.</summary>
    MissionObject GetSupportHull(Agent agent);

    /// <summary>The hull a usable point belongs to and the point's content path below that hull; false off ships.</summary>
    bool TryDescribeStation(UsableMissionObject point, out MissionObject hull, out string stationKey, out int pointIndex);

    /// <summary>The usable point at <paramref name="stationKey"/> below <paramref name="hull"/>; null when it is not there.</summary>
    UsableMissionObject ResolveStation(MissionObject hull, string stationKey, int pointIndex);

    /// <summary>[Game thread] Seats a puppet at, or releases it from, a station the way the owner did.</summary>
    void ApplyStationUse(Agent agent, UsableMissionObject point, bool inUse);

    /// <summary>Whether the agent is the point's user and uses it.</summary>
    bool IsSeated(Agent agent, UsableMissionObject point);

    /// <summary>Whether the agent is still an active mission agent.</summary>
    bool IsAlive(Agent agent);

    /// <summary>[Game thread] Re-targets a seated puppet at its station's user frame on the current hull frame.</summary>
    void PinToStation(Agent agent, UsableMissionObject point);

}
