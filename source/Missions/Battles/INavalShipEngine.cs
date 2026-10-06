using Missions.Messages;
using System;
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

    /// <summary>[Game thread] Hands an AI (NPC) hull's simulation away from this client: it becomes a kinematic copy following its owner's samples.</summary>
    void ReleaseNpcHull(MissionObject hull);

    /// <summary>
    /// [Mission tick] First step of replacing a hull: stops every agent aboard using its machines and parks it off
    /// every hull, and stops tracking the hull's crew, so nobody stands on or is tracked by the hull when it goes.
    /// </summary>
    IReadOnlyList<HullSwapAgent> ParkHullAgents(MissionObject hull);

    /// <summary>
    /// [Mission tick] Second step: removes the hull and spawns a fresh simulated hull of the same ship at the same frame
    /// under the AI controller, because a copy's body cannot be re-activated. Null when the hull cannot be replaced.
    /// </summary>
    MissionObject ReplaceHull(MissionObject hull);

    /// <summary>
    /// [Mission tick] Last step: moves the parked agents back to their deck positions on <paramref name="hull"/>, makes
    /// the crew its crew again and seats the helm and oars.
    /// </summary>
    void BoardHullAgents(MissionObject hull, IReadOnlyList<HullSwapAgent> agents);

    /// <summary>Whether an agent is seated at the hull's helm (diagnostics).</summary>
    bool HasHelmPilot(MissionObject hull);

    /// <summary>[Game thread] The ropes this client threw from an own hull, keyed by the registered ids of the hulls they target.</summary>
    BattleRopeState[] CaptureRopes(MissionObject hull, Func<MissionObject, Guid> shipIdOf);

    /// <summary>[Game thread] Replays another owner's ropes on its copied hull; <paramref name="final"/> lays them out once more for good.</summary>
    void ApplyRopes(MissionObject hull, BattleRopeState[] ropes, Func<Guid, MissionObject> hullOf, bool final);

    /// <summary>[Game thread] Rope state per throw station and the joint force counters (diagnostics).</summary>
    object InspectRopes(IEnumerable<MissionObject> hulls, Func<MissionObject, Guid> shipIdOf);

    /// <summary>
    /// [Game thread] Replays another client's hit on a hull this client simulates through the vanilla damage method of its
    /// kind; <paramref name="attacker"/> and <paramref name="hitter"/> are null when they are not known here.
    /// </summary>
    void ApplyShipDamage(MissionObject hull, NetworkApplyShipDamage damage, Agent attacker, MissionObject hitter);

    /// <summary>The hull's current damage state.</summary>
    BattleShipCondition ReadCondition(MissionObject hull);

    /// <summary>Whether the hull is sinking or has sunk.</summary>
    bool IsSinking(MissionObject hull);

    /// <summary>
    /// [Game thread] Sets an owner's damage state on a copied hull, or carries it onto a replacement hull: HP, fire and
    /// sails, and sinking through the vanilla path. A copy also takes the sunk state; a simulated hull sinks on its own.
    /// </summary>
    void ApplyCondition(MissionObject hull, BattleShipCondition condition);
}

/// <summary>An agent a hull replacement moves: whether it is the hull's crew and, when it stood aboard, its hull-local deck position.</summary>
public sealed class HullSwapAgent
{
    public HullSwapAgent(Agent agent, bool isCrew, bool isParked, Vec3 deckLocal)
    {
        Agent = agent;
        IsCrew = isCrew;
        IsParked = isParked;
        DeckLocal = deckLocal;
    }

    public Agent Agent { get; }
    public bool IsCrew { get; }
    public bool IsParked { get; }
    public Vec3 DeckLocal { get; }
}
