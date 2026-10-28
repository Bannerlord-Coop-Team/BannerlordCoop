using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
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

    /// <summary>
    /// Whether NavalShipsLogic is still in deployment, where a removed hull is pooled and spawning its ship again returns
    /// that same hull, so a hull cannot be replaced yet.
    /// </summary>
    bool IsDeploymentMode { get; }

    /// <summary>The hull's campaign ship description, side, formation and frame under the given identity.</summary>
    BattleShipSpawnData Describe(MissionObject hull, NetworkShipInfo identity);

    /// <summary>
    /// Spawns a copy of another owner's hull on <paramref name="team"/>: its body keeps simulating and is steered toward the
    /// owner's samples. Null when it cannot.
    /// </summary>
    MissionObject SpawnForeignHull(BattleShipSpawnData data, Team team, out Formation formation);

    MatrixFrame GetFrame(MissionObject hull);

    /// <summary>
    /// [Game thread] Takes the owner's latest sample of a copy as the target its steering closes on, and the owner's crew load
    /// the copy heels under.
    /// </summary>
    void ApplyForeignSample(MissionObject hull, MatrixFrame frame, BattleShipMotion motion);

    /// <summary>The hull body's planar velocity, yaw rate and committed crew load, sent with its samples.</summary>
    BattleShipMotion ReadMotion(MissionObject hull);

    /// <summary>The hull's effective helm input this frame.</summary>
    BattleShipInput ReadInput(MissionObject hull);

    /// <summary>Drives a copied hull's oars and sails with its owner's helm input.</summary>
    void ApplyInput(MissionObject hull, BattleShipInput input);

    /// <summary>"own", or how a copy follows its owner here: "steered" while its body simulates, "written" otherwise (diagnostics).</summary>
    string BodyMode(MissionObject hull);

    /// <summary>The hull's ship controller type name (diagnostics).</summary>
    string ControllerName(MissionObject hull);

    /// <summary>The hull an on-foot agent stands on: its stepped ship, or a connected plank's source hull; null off ships.</summary>
    MissionObject GetSupportHull(Agent agent);

    /// <summary>The hull's planar footprint in its local frame, from vanilla's physics bounding box; false when it has none.</summary>
    bool TryGetFootprint(MissionObject hull, out Vec2 min, out Vec2 max);

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

    /// <summary>Whether the agent is this client's main agent, the player's hero.</summary>
    bool IsMainAgent(Agent agent);

    /// <summary>[Game thread] Re-targets a seated puppet at its station's user frame on the current hull frame.</summary>
    void PinToStation(Agent agent, UsableMissionObject point);

    /// <summary>[Game thread] Hands a hull's simulation away from this client: it becomes a copy steered toward its owner's samples.</summary>
    void ReleaseHull(MissionObject hull);

    /// <summary>
    /// [Game thread] Takes over simulating a copy in place, as a capture's new authority: it stops following samples and gets an
    /// auto-updated controller vanilla's rules pick from its formation. Nothing is removed or respawned.
    /// </summary>
    void AdoptHull(MissionObject hull);

    /// <summary>
    /// [Mission tick] First step of replacing or removing a hull: stops every agent aboard using its machines and parks it off
    /// every hull, and stops tracking the hull's crew, so nobody stands on or is tracked by the hull when it goes.
    /// </summary>
    IReadOnlyList<HullSwapAgent> ParkHullAgents(MissionObject hull);

    /// <summary>
    /// [Mission tick] Second step: removes the hull and spawns a fresh simulated hull of the same ship at the same frame
    /// under the AI controller, so the new authority simulates a hull vanilla spawned. Null when the hull cannot be replaced.
    /// </summary>
    MissionObject ReplaceHull(MissionObject hull);

    /// <summary>
    /// [Mission tick] Last step: moves the parked agents back to their deck positions on <paramref name="hull"/>, makes
    /// the crew its crew again and seats the helm and oars. With a null or removed hull, the parked agents go back where they stood.
    /// </summary>
    void BoardHullAgents(MissionObject hull, IReadOnlyList<HullSwapAgent> agents);

    /// <summary>Whether an agent is seated at the hull's helm (diagnostics).</summary>
    bool HasHelmPilot(MissionObject hull);

    /// <summary>[Game thread] The active humans standing on the hull's deck or a plank it threw, or using one of its machines.</summary>
    IReadOnlyList<Agent> AgentsOnDeck(MissionObject hull);

    /// <summary>The hull the agent crews: the hull of its formation; null when its formation has none.</summary>
    MissionObject CrewHullOf(Agent agent);

    /// <summary>
    /// [Game thread] Tracks the agent on <paramref name="hull"/> (unless already tracked) and points its FormationShip at it,
    /// which vanilla's off-ship check assumes.
    /// </summary>
    void EnlistCrew(Agent agent, MissionObject hull);

    /// <summary>The nearest afloat hull of a team friendly to the agent's, other than <paramref name="excluded"/>; null when none.</summary>
    MissionObject NearestFriendlyHull(Agent agent, IReadOnlyCollection<MissionObject> excluded);

    /// <summary>[Game thread] Moves an on-foot agent onto the hull's deck where vanilla places deck crew.</summary>
    void MoveAgentToHull(Agent agent, MissionObject hull);

    /// <summary>[Game thread] Stops every agent still using one of the hull's machines, so none keeps a user once it goes.</summary>
    void ReleaseHullStations(MissionObject hull);

    /// <summary>
    /// [Game thread] Takes a hull out of the mission through vanilla's RemoveShip, after breaking every rope and plank that
    /// starts or ends on it.
    /// </summary>
    void RemoveHull(MissionObject hull);

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

    /// <summary>Troops queued on a hull this client simulates that can still field; none on a copy, whose queue lives on its owner.</summary>
    int ReadSpawnableReserve(MissionObject hull);

    /// <summary>The battle side of the hull's team.</summary>
    BattleSideEnum SideOf(MissionObject hull);

    /// <summary>
    /// [Game thread] Queues troops on an afloat hull this client simulates, in order and up to its crew capacity, and has its
    /// team spawn them as reinforcements. How many it took; none on a copy.
    /// </summary>
    int AddHullReserve(MissionObject hull, IReadOnlyList<IAgentOriginBase> origins);

    /// <summary>The object-manager id of the campaign ship the hull stands for, or null when it stands for none.</summary>
    string CampaignShipIdOf(MissionObject hull);

    /// <summary>The damage the hull's ship upgrades mitigated on hits this client applied so far, the base of Boatswain XP.</summary>
    float ReadMitigatedDamage(MissionObject hull);

    /// <summary>
    /// [Game thread] Takes an agent out of the battle the way vanilla takes a removed hull's crew: it fades out, which reads as
    /// a rout, not a casualty.
    /// </summary>
    void WithdrawAgent(Agent agent);

    /// <summary>Whether the hull is sinking or has sunk.</summary>
    bool IsSinking(MissionObject hull);

    /// <summary>Whether the hull left the mission: removed, pooled by deployment, or its entity is gone.</summary>
    bool IsHullRemoved(MissionObject hull);

    /// <summary>
    /// [Game thread] Sets an owner's damage state on a copied hull, or carries it onto a replacement hull: HP, fire and
    /// sails, and sinking through the vanilla path. A copy also takes the sunk state; a simulated hull sinks on its own.
    /// </summary>
    void ApplyCondition(MissionObject hull, BattleShipCondition condition);

    /// <summary>The hull's ship siege weapon and its content path below the hull; false when the hull carries none.</summary>
    bool TryGetWeapon(MissionObject hull, out MissionObject weapon, out string weaponKey);

    /// <summary>The aim targets, weapon state and ammo of a ship siege weapon.</summary>
    BattleShipWeaponState ReadWeaponState(MissionObject weapon);

    /// <summary>
    /// [Game thread] Turns, loads and animates a weapon this client does not simulate the way its owner's state says;
    /// <paramref name="previous"/> is the state applied before, or null.
    /// </summary>
    void ApplyWeaponState(MissionObject weapon, BattleShipWeaponState state, BattleShipWeaponState previous);

    /// <summary>
    /// [Game thread] Plays an owner's shot on a weapon this client does not simulate, with a projectile that deals no
    /// damage here; with no <paramref name="shooter"/> any inert puppet fires it. The release animation and sound play once
    /// per volley, when <paramref name="playLaunchEffects"/>. False when it could not be shown.
    /// </summary>
    bool ReplayWeaponShot(MissionObject weapon, NetworkShipWeaponShot shot, Agent shooter, bool playLaunchEffects);

    /// <summary>[Game thread] Simulates only <paramref name="own"/> ship weapons here; every other one follows its owner.</summary>
    void SetSimulatedWeapons(IReadOnlyCollection<MissionObject> own);

    /// <summary>[Game thread] Hands ship weapon simulation back to the defaults when the battle ends.</summary>
    void ClearSimulatedWeapons();
}

/// <summary>An agent a hull replacement moves: whether it is the hull's crew and, when it stood aboard, its hull-local deck position.</summary>
public sealed class HullSwapAgent
{
    public HullSwapAgent(Agent agent, bool isCrew, bool isParked, Vec3 deckLocal, Vec3 parkedFrom)
    {
        Agent = agent;
        IsCrew = isCrew;
        IsParked = isParked;
        DeckLocal = deckLocal;
        ParkedFrom = parkedFrom;
    }

    public Agent Agent { get; }
    public bool IsCrew { get; }
    public bool IsParked { get; }
    public Vec3 DeckLocal { get; }
    /// <summary>The agent's world position before it was parked.</summary>
    public Vec3 ParkedFrom { get; }
}
