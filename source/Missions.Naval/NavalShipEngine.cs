using Common.Logging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using SinkingState = NavalDLC.Missions.NavalPhysics.NavalPhysics.SinkingState;

namespace Missions.Naval;

/// <inheritdoc cref="INavalShipEngine"/>
public class NavalShipEngine : INavalShipEngine
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalShipEngine>();

    private const int MaxPathDepth = 16;

    // Clear of every hull and the water, where NavalTeamAgents.UnassignAgentAux parks troops it takes off a ship.
    private const float ParkingHeight = 500f;

    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;
    private readonly INavalAgentTickGate agentTickGate;
    // [Game thread] The last state each copy's weapon took from its authority; a copy's own State never follows it.
    private readonly Dictionary<MissionObject, BattleShipWeaponState> replicatedWeaponStates =
        new Dictionary<MissionObject, BattleShipWeaponState>();

    public NavalShipEngine(ICoopShipSnapshotBuilder snapshotBuilder, IObjectManager objectManager, INavalSteeredCopies steeredCopies,
        INavalAgentTickGate agentTickGate)
    {
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
        SteeredCopies = steeredCopies;
        this.agentTickGate = agentTickGate;
    }

    /// <summary>This engine's copies and their steering knobs (the DEBUG probe and copy_steer read them).</summary>
    internal INavalSteeredCopies SteeredCopies { get; }

    private static NavalShipsLogic ShipsLogic => Mission.Current?.GetMissionBehavior<NavalShipsLogic>();

    public IReadOnlyList<MissionObject> Hulls =>
        ShipsLogic?.AllShips.Cast<MissionObject>().ToList() ?? new List<MissionObject>();

    public bool IsDeploymentMode => ShipsLogic?.IsDeploymentMode == true;

    public BattleShipSpawnData Describe(MissionObject hull, NetworkShipInfo identity)
    {
        var ship = (MissionShip)hull;
        var campaignShip = ship.ShipOrigin as Ship;
        var pieces = campaignShip?._shipPieces.Where(piece => piece.Value != null).ToArray()
            ?? Array.Empty<KeyValuePair<string, ShipUpgradePiece>>();

        // A capture's orphan has no team or formation left, so its record names the side it had and any formation.
        return new BattleShipSpawnData(
            identity.ShipId,
            identity.CurrentAuthority,
            identity.MapEventPartyId,
            identity.IsNpcParty,
            ship.Team?.Side ?? identity.OrphanedSide,
            ship.Formation == null ? 0 : (int)ship.Formation.FormationIndex,
            NetworkBattleShipSample.FromFrame(ship.GlobalFrame),
            ship.ShipOrigin.Hull.StringId,
            pieces.Select(piece => piece.Key).ToArray(),
            pieces.Select(piece => piece.Value.StringId).ToArray(),
            campaignShip?.Figurehead?.StringId,
            campaignShip?._name?.ToString(),
            ship.ShipOrigin.HitPoints,
            ship.ShipOrigin.SailHitPoints,
            ship.ShipOrigin.RandomValue,
            ship.ShipOrigin.CustomSailPatternId,
            identity.OriginalOwner,
            CampaignShipIdOf(hull),
            identity.CaptorControllerId,
            identity.IsOrphaned,
            identity.CaptureSequence,
            identity.Capture?.CaptorShipId ?? Guid.Empty,
            identity.Capture?.FromSide ?? BattleSideEnum.None);
    }

    public MissionObject SpawnForeignHull(BattleShipSpawnData data, Team team, out Formation formation)
    {
        formation = null;
        var shipsLogic = ShipsLogic;
        if (shipsLogic == null || !NetworkBattleShipSample.IsValidFrame(data.Frame)) return null;

        // NavalShipsLogic keys hulls by team side and formation class, so a team holds at most one hull per formation.
        formation = shipsLogic.FindFirstFormationWithoutShip(team, (FormationClass)8);
        if (formation == null) return null;

        objectManager.TryGetObject(data.MapEventPartyId, out MapEventParty mapEventParty);
        var origin = snapshotBuilder.Build(data, mapEventParty?.Party);
        if (origin == null) return null;

        var frame = NetworkBattleShipSample.ToFrame(data.Frame);
        var ship = shipsLogic.SpawnShip(origin, in frame, team, formation, spawnAnchored: false, checkForFreeArea: false);

        // The owner decides this hull: no local controller and no auto controller; its body is steered toward the samples.
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
        SteeredCopies.Track(ship);
        OpenEmptyEnemyHelm(ship, Mission.Current?.PlayerTeam);
        formation = ship.Formation;
        return ship;
    }

    // The late hull patch made an enemy copy's helm AI-only, and vanilla only opens it to the player when a unit leaves its
    // formation; a copy's formation may never get one here (a late joiner's emptied AI hull), and puppets never pick a station.
    private static void OpenEmptyEnemyHelm(MissionShip ship, Team playerTeam)
    {
        if (!OpensHelmToPlayer(playerTeam != null, ship.BattleSide == playerTeam?.Side, ship.Formation?.CountOfUnits ?? 0)) return;

        ship.ShipControllerMachine.PilotStandingPoint.SetUsableByPlayerOnly();
    }

    /// <summary>
    /// Whether a fresh copy's helm opens to the player at once, as MissionShip.OnFormationUnitRemoved opens an enemy helm whose
    /// formation emptied: an enemy hull with no unit in its formation. Vanilla still disables it for players until it is vacant.
    /// </summary>
    internal static bool OpensHelmToPlayer(bool hasPlayerTeam, bool isPlayerSide, int formationUnits) =>
        hasPlayerTeam && !isPlayerSide && formationUnits == 0;

    // The async agent tick reads the hull's controller and the copy set, so it finishes before either changes.
    public void ReleaseHull(MissionObject hull)
    {
        agentTickGate.WaitForAgentTick("a hull is handed away");
        var ship = (MissionShip)hull;
        SteeredCopies.Track(ship);
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
    }

    // A steered copy keeps its dynamic body, so dropping it from the copy set is all a new authority needs to simulate it.
    public void AdoptHull(MissionObject hull)
    {
        agentTickGate.WaitForAgentTick("a hull is adopted");
        var ship = (MissionShip)hull;
        if (!NavalSteeredCopies.IsSimulating(ship))
            Logger.Warning("[NavalSync] Adopted hull {Index} has no simulating body; it stays where its last sample put it", ship.Index);

        SteeredCopies.Forget(ship);
        ship.SetController(ShipControllerType.None, autoUpdateController: true);
    }

    // Vanilla only removes a hull nobody it keeps stands on (Quest5 frees crewless hulls, ShipRetreatLogic fades the crew
    // aboard), so the agents aboard leave a tick before the hull does, parked the way UnassignAgentAux parks troops.
    public IReadOnlyList<HullSwapAgent> ParkHullAgents(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var mission = Mission.Current;
        var agentsLogic = mission?.GetMissionBehavior<NavalAgentsLogic>();
        if (agentsLogic == null || !ship.GameEntity.IsValid) return Array.Empty<HullSwapAgent>();

        var frame = ship.GlobalFrame;
        var moved = new List<HullSwapAgent>();
        foreach (var agent in mission.Agents.ToList())
        {
            if (!agent.IsActive() || !agent.IsHuman) continue;

            bool isTracked = agentsLogic.IsAgentOnAnyShip(agent, out var onShip) && onShip == ship;
            bool isCrew = IsHullCrew(isTracked, agent.Team == ship.Team, ship.Formation != null && agent.Formation == ship.Formation);
            bool isAboard = ship.GetIsAgentOnShip(agent) || agent.GetComponent<AgentNavalComponent>()?.SteppedShip == ship
                || UsesMachineOf(agent, ship);
            if (!isCrew && !isAboard) continue;

            if (isAboard && agent.CurrentlyUsedGameObject != null)
                agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
            if (isTracked) agentsLogic.RemoveAgentFromShip(agent, ship);

            var position = agent.Position;
            var deckLocal = Vec3.Zero;
            if (isAboard)
            {
                deckLocal = frame.TransformToLocalNonOrthogonal(position);
                agent.SetIsPhysicsForceClosed(true);
                agent.TeleportToPosition(new Vec3(position.x, position.y, ParkingHeight));
            }

            moved.Add(new HullSwapAgent(agent, isCrew, isAboard, deckLocal, position));
        }

        return moved;
    }

    /// <summary>
    /// Whether an agent crews a hull: it is in the hull's formation, or vanilla tracks it on the hull and it is on the hull's
    /// team. Vanilla's capture swap points the old owner's crew record at the captor's orphan, which has no team, so the old
    /// owner's swimmers never count as the orphan's crew.
    /// </summary>
    internal static bool IsHullCrew(bool isTracked, bool isOnHullsTeam, bool isInHullsFormation) =>
        isInHullsFormation || (isTracked && isOnHullsTeam);

    private static bool UsesMachineOf(Agent agent, MissionShip ship) =>
        agent.CurrentlyUsedGameObject is UsableMissionObject used && used.GameEntity.IsValid && used.GameEntity.Root == ship.GameEntity;

    // The new authority simulates a hull spawned the way the host spawned it, not the copy it steered.
    public MissionObject ReplaceHull(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var mission = Mission.Current;
        var shipsLogic = ShipsLogic;
        var formation = ship.Formation;
        var team = ship.Team;
        if (shipsLogic == null || formation == null || team == null || !ship.GameEntity.IsValid) return null;

        var origin = ship.ShipOrigin;
        var frame = ship.GlobalFrame;
        var condition = ReadCondition(ship);
        var weapon = ship.ShipSiegeWeapon;
        int? ammo = weapon == null ? (int?)null : CarriedAmmo(weapon.AmmoCount, weapon.State, TakeReplicatedState(weapon));

        // Quest5's RemoveShipInternal: no rope or bridge may keep pointing at a removed hull, as in RemoveHull.
        NavalRopes.BreakConnections(ship);
        SteeredCopies.Forget(ship);
        shipsLogic.RemoveShip(ship);

        var fresh = shipsLogic.SpawnShip(origin, in frame, team, formation, spawnAnchored: false, checkForFreeArea: false);
        fresh.SetController(ShipControllerType.AI);
        formation.SetControlledByAI(true);

        // HP lives on the shared origin; fire, partial HP and sinking live on the hull, so a sinking hull keeps sinking.
        ApplyCondition(fresh, condition);
        // A spawned weapon starts loaded with its full reserve (RangedSiegeWeapon.OnInit), so the bolts already shot stay spent.
        if (ammo.HasValue) fresh.ShipSiegeWeapon?.SetAmmo(ammo.Value);

        // Removing one hull and adding one leaves the planner's agent count unchanged, so its k-d tree keeps the removed
        // hull and never sees the new one. Rebuild it the way vanilla does after changing ships mid-mission.
        mission.GetMissionBehavior<NavalTrajectoryPlanningLogic>()?.ForceReinitialize();
        return fresh;
    }

    /// <summary>
    /// The reserve a fresh, loaded weapon takes over: AmmoCount leaves out the loaded bolt and a reload only spends its bolt as it
    /// ends (Ballista's ConsumeAmmo on Idle), so a weapon caught mid-reload gives one bolt to the fresh weapon's load. An empty
    /// weapon caught mid-reload still comes back loaded with one bolt it didn't have.
    /// </summary>
    internal static int CarriedAmmo(int ammoCount, bool isLoaded) => isLoaded ? ammoCount : Math.Max(0, ammoCount - 1);

    /// <summary>
    /// The reserve a swap carries, read from the authority's last replicated state when the weapon was a copy here (its local
    /// State never leaves Idle), otherwise from the weapon's own state.
    /// </summary>
    internal static int CarriedAmmo(int localAmmo, RangedSiegeWeapon.WeaponState localState, BattleShipWeaponState replicated) =>
        replicated == null
            ? CarriedAmmo(localAmmo, localState == RangedSiegeWeapon.WeaponState.Idle)
            : CarriedAmmo(replicated.AmmoCount, replicated.WeaponState == (int)RangedSiegeWeapon.WeaponState.Idle);

    // Takes the replicated state a weapon about to be replaced was following, or null for one this client simulated.
    private BattleShipWeaponState TakeReplicatedState(MissionObject weapon)
    {
        if (!replicatedWeaponStates.TryGetValue(weapon, out var replicated)) return null;

        replicatedWeaponStates.Remove(weapon);
        return replicated;
    }

    public void BoardHullAgents(MissionObject hull, IReadOnlyList<HullSwapAgent> agents)
    {
        var ship = hull as MissionShip;
        var agentsLogic = Mission.Current?.GetMissionBehavior<NavalAgentsLogic>();
        if (agents == null) return;
        if (agentsLogic == null || ship == null || ship.IsRemoved || !ship.GameEntity.IsValid || ship.Team == null)
        {
            UnparkWhereTheyStood(agents);
            return;
        }

        var frame = ship.GlobalFrame;
        foreach (var moved in agents)
        {
            var agent = moved.Agent;
            if (!agent.IsActive()) continue;

            if (moved.IsParked)
            {
                agent.SetIsPhysicsForceClosed(false);
                agent.TeleportToPosition(frame.TransformToParent(moved.DeckLocal));
            }

            if (moved.IsCrew) agentsLogic.AddAgentToShip(agent, ship);
        }

        // Vanilla seats the helm and oars and moves their crew onto them.
        agentsLogic.AssignAndTeleportCrewToShipMachines(ship);
    }

    // With no hull left to board, the parked agents go back where they stood rather than hang frozen at the parking height.
    private static void UnparkWhereTheyStood(IReadOnlyList<HullSwapAgent> agents)
    {
        foreach (var moved in agents)
        {
            var agent = moved.Agent;
            if (!moved.IsParked || !agent.IsActive()) continue;

            agent.SetIsPhysicsForceClosed(false);
            agent.TeleportToPosition(moved.ParkedFrom);
        }
    }

    public bool HasHelmPilot(MissionObject hull) => ((MissionShip)hull).ShipControllerMachine?.PilotAgent != null;

    public IReadOnlyList<Agent> AgentsOnDeck(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var mission = Mission.Current;
        if (mission == null || !ship.GameEntity.IsValid) return Array.Empty<Agent>();

        return mission.Agents
            .Where(agent => agent.IsActive() && agent.IsHuman && (GetSupportHull(agent) == ship || UsesMachineOf(agent, ship)))
            .ToList();
    }

    public MissionObject CrewHullOf(Agent agent)
    {
        var formation = agent?.Formation;
        var shipsLogic = ShipsLogic;
        if (formation == null || shipsLogic == null) return null;

        return shipsLogic.GetShip(formation, out var ship) ? ship : null;
    }

    public MissionObject NearestFriendlyHull(Agent agent, IReadOnlyCollection<MissionObject> excluded)
    {
        var shipsLogic = ShipsLogic;
        var team = agent?.Team;
        if (shipsLogic == null || team == null) return null;

        MissionShip nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var ship in shipsLogic.AllShips)
        {
            if (excluded.Contains(ship) || ship.IsRemoved || ship.IsSinking || !ship.GameEntity.IsValid || ship.Team == null
                || !ship.Team.IsFriendOf(team))
                continue;

            float distance = ship.GlobalFrame.origin.DistanceSquared(agent.Position);
            if (distance >= nearestDistance) continue;

            nearest = ship;
            nearestDistance = distance;
        }

        return nearest;
    }

    // Vanilla spawns deck crew at the hull's outer and inner deck frames (NavalShipAgents), so a moved agent lands on one too.
    public void MoveAgentToHull(Agent agent, MissionObject hull)
    {
        var ship = (MissionShip)hull;
        if (!agent.IsActive() || ship.IsRemoved || !ship.GameEntity.IsValid) return;

        Vec3 position;
        if (ship.DeckFrameCount > 0)
        {
            position = ship.GetNextOuterInnerSpawnGlobalFrame().origin;
        }
        else
        {
            ship.GetWorldPositionOnDeck(out var deck);
            position = deck.GetGroundVec3();
        }

        agent.TeleportToPosition(position);
    }

    public void ReleaseHullStations(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var mission = Mission.Current;
        if (mission == null || !ship.GameEntity.IsValid) return;

        foreach (var agent in mission.Agents.Where(agent => agent.IsActive() && UsesMachineOf(agent, ship)).ToList())
            agent.StopUsingGameObject(isSuccessful: false, Agent.StopUsingGameObjectFlags.None);
    }

    public void RemoveHull(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var shipsLogic = ShipsLogic;
        if (shipsLogic == null || ship.IsRemoved || !ship.GameEntity.IsValid) return;

        // Quest5's RemoveShipInternal: no rope or bridge may keep pointing at a removed hull. A copy stays a copy until the
        // behavior's ShipRemovedEvent handler drops it, so the copy gates hold for the whole removal.
        NavalRopes.BreakConnections(ship);
        switch (RemovalPathOf(shipsLogic, ship))
        {
            case HullRemovalPath.Assigned:
                shipsLogic.RemoveShip(ship);
                return;
            case HullRemovalPath.Unassigned:
                RemoveUnassignedHull(shipsLogic, ship);
                return;
            default:
                // Vanilla removes whatever hull holds the origin's assignment, so that stranger must not go in this hull's place.
                Logger.Error("[NavalSync] Hull {Index} was not removed: its ship origin is assigned to another hull", ship.Index);
                return;
        }
    }

    /// <summary>
    /// How vanilla's removal can take a hull: through the assignment holding it, through a lent one when none holds its origin
    /// (a capture's orphan), or not at all when another hull holds its origin, which vanilla would remove instead.
    /// </summary>
    internal static HullRemovalPath RemovalPathOf(NavalShipsLogic shipsLogic, MissionShip ship)
    {
        if (!shipsLogic.FindAssignmentOfShipOrigin(ship.ShipOrigin, out var held)) return HullRemovalPath.Unassigned;

        return held.MissionShip == ship ? HullRemovalPath.Assigned : HullRemovalPath.OriginHeldByAnotherHull;
    }

    // NavalShipsLogic.RemoveShip only acts through the assignment holding a hull, and a capture's orphan holds none (OnShipCaptured
    // gave it to the prize), so the orphan borrows a free one for vanilla's RemoveShipAux.
    private static void RemoveUnassignedHull(NavalShipsLogic shipsLogic, MissionShip ship)
    {
        var outcome = RemoveThroughLentAssignment(shipsLogic._shipAssignments, ship, shipsLogic.RemoveShip);
        if (outcome == LentRemoval.Removed)
            Logger.Information("[NavalSync] Removed hull {Index}, which held no ship assignment, through a free one", ship.Index);
        else if (outcome == LentRemoval.NoFreeAssignment)
            Logger.Error("[NavalSync] Hull {Index} holds no ship assignment and none is free, so it was not removed", ship.Index);
        else
            Logger.Error("[NavalSync] Hull {Index} holds no ship assignment and vanilla's removal did not take it", ship.Index);
    }

    /// <summary>
    /// Lends <paramref name="ship"/> a free assignment, runs vanilla's removal through it, and frees the slot again however the
    /// removal ended (RemoveShipAux clears it, a throwing removal handler would not), so the slot never keeps the hull.
    /// </summary>
    internal static LentRemoval RemoveThroughLentAssignment(NavalDLC.ShipAssignment[,] assignments, MissionShip ship,
        Action<MissionShip> removeShip)
    {
        var lent = LendFreeAssignment(assignments, ship);
        if (lent == null) return LentRemoval.NoFreeAssignment;

        try
        {
            removeShip(ship);
        }
        finally
        {
            if (lent.MissionShip == ship) lent.Clear();
        }

        return ship.IsRemoved ? LentRemoval.Removed : LentRemoval.NotRemoved;
    }

    /// <summary>
    /// Points the first free ship assignment (no origin, no hull) at <paramref name="ship"/>, so vanilla's removal finds the hull
    /// through it; null when every assignment is taken.
    /// </summary>
    internal static NavalDLC.ShipAssignment LendFreeAssignment(NavalDLC.ShipAssignment[,] assignments, MissionShip ship)
    {
        foreach (var assignment in assignments)
        {
            if (assignment == null || assignment.ShipOrigin != null || assignment.MissionShip != null) continue;

            assignment.SetMissionShip(ship);
            return assignment;
        }

        return null;
    }

    public MatrixFrame GetFrame(MissionObject hull) => ((MissionShip)hull).GlobalFrame;

    public void ApplyForeignSample(MissionObject hull, MatrixFrame frame, BattleShipMotion motion) =>
        SteeredCopies.Observe((MissionShip)hull, frame, motion);

    public BattleShipMotion ReadMotion(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var entity = ship.GameEntity;
        if (!entity.IsValid) return default;

        return new BattleShipMotion(entity.GetLinearVelocity().AsVec2, entity.GetAngularVelocity().z, ReadCrewLoad(ship));
    }

    /// <summary>
    /// The crew load vanilla's <c>ApplyAgentForces</c> applies to the hull this step: the committed agent mass and its centre
    /// in the body's local frame. Null when it is not known this step.
    /// </summary>
    internal static BattleShipCrewLoad ReadCrewLoad(MissionShip ship)
    {
        var physics = ship.Physics;
        if (physics == null || !ship.GameEntity.IsValid) return null;

        float mass = physics._committedTotalMass;
        return CrewLoadOf(mass, physics._committedWeightedAgentsPosition, ship.GameEntity.GetBodyWorldTransform(),
            mass > 0f || HasAgentAboard(ship));
    }

    /// <summary>
    /// Vanilla's crew centre: the mass-weighted world position over the mass, moved into the body frame. No mass reads as an
    /// empty deck only with nobody aboard; with agents aboard it is a frame that committed none (dt 0), so unknown.
    /// </summary>
    internal static BattleShipCrewLoad CrewLoadOf(float mass, Vec3 weightedPosition, MatrixFrame body, bool agentAboard)
    {
        if (mass > 0f) return BattleShipCrewLoad.Create(mass, body.TransformToLocal(weightedPosition / mass));

        return agentAboard ? null : BattleShipCrewLoad.Empty;
    }

    // Whether an active agent stands on the hull, the agents vanilla's AgentNavalComponent.OnTick weighs it with.
    private static bool HasAgentAboard(MissionShip ship)
    {
        var mission = Mission.Current;
        if (mission == null) return false;

        foreach (var agent in mission.Agents)
        {
            if (agent.IsActive() && agent.GetComponent<AgentNavalComponent>()?.SteppedShip == ship) return true;
        }

        return false;
    }

    public void TickCopies(float fixedDt) => SteeredCopies.Tick(fixedDt);

    public void SnapCopies() => SteeredCopies.ApplySnaps();

    public void ForgetHull(MissionObject hull)
    {
        if (hull is MissionShip ship) SteeredCopies.Forget(ship);
    }

    public BattleShipInput ReadInput(MissionObject hull)
    {
        var input = ((MissionShip)hull)._inputRecord;
        return new BattleShipInput((int)input.RowerLateral, (int)input.RowerLongitudinal,
            (int)input.RowerLongitudinalDoubleTap, input.RudderLateral, (int)input.Sail);
    }

    // A copy has no controller, so nothing overwrites this record before its actuators read it.
    public void ApplyInput(MissionObject hull, BattleShipInput input)
    {
        if (!Enum.IsDefined(typeof(RowerLateralInput), input.RowerLateral)
            || !Enum.IsDefined(typeof(RowerLongitudinalInput), input.RowerLongitudinal)
            || !Enum.IsDefined(typeof(RowerLongitudinalInput), input.RowerLongitudinalDoubleTap)
            || !Enum.IsDefined(typeof(SailInput), input.Sail))
            return;

        var record = new ShipInputRecord((RowerLateralInput)input.RowerLateral, (RowerLongitudinalInput)input.RowerLongitudinal,
            (RowerLongitudinalInput)input.RowerLongitudinalDoubleTap, input.Rudder, (SailInput)input.Sail);
        ((MissionShip)hull).SetInputRecord(in record);
    }

    public string BodyMode(MissionObject hull) => NavalSteeredCopies.ModeName((MissionShip)hull);

    public string ControllerName(MissionObject hull) =>
        ((MissionShip)hull).Controller?.ControllerType.ToString() ?? ShipControllerType.None.ToString();

    public MissionObject GetSupportHull(Agent agent)
    {
        if (agent == null || !agent.IsActive() || agent.HasMount) return null;

        var stepped = agent.GetComponent<AgentNavalComponent>()?.SteppedShip;
        if (stepped != null && stepped.GetIsAgentOnShip(agent)) return stepped;

        return PlankSourceHull(agent);
    }

    // The box vanilla reads the hull outline from (MissionShip's XY plane vertices), in the hull's local frame.
    public bool TryGetFootprint(MissionObject hull, out Vec2 min, out Vec2 max)
    {
        min = max = Vec2.Zero;
        var physics = (hull as MissionShip)?.Physics;
        if (physics == null) return false;

        var box = physics.PhysicsBoundingBoxWithoutChildren;
        min = box.min.AsVec2;
        max = box.max.AsVec2;
        return max.x > min.x && max.y > min.y;
    }

    // A connected plank belongs to no hull; its rope's source hull is the reference frame.
    private static MissionShip PlankSourceHull(Agent agent)
    {
        var shipsLogic = ShipsLogic;
        if (shipsLogic == null) return null;

        foreach (var ship in shipsLogic.AllShips)
        {
            foreach (var machine in ship.AttachmentMachines)
            {
                var attachment = machine.CurrentAttachment;
                if (attachment?.State == ShipAttachmentMachine.ShipAttachment.ShipAttachmentState.BridgeConnected
                    && IsOnPlank(agent, attachment))
                    return ship;
            }
        }

        return null;
    }

    internal static bool IsOnPlank(Agent agent, ShipAttachmentMachine.ShipAttachment attachment)
    {
        int face = agent.GetCurrentNavigationFaceId();
        if (attachment._navMeshBridge != null && face >= attachment._bridgeNavmeshId && face <= attachment._bridgeNavmeshId + 4)
            return true;

        var stepped = agent.GetSteppedEntity();
        if (!stepped.IsValid) return false;

        var bridge = attachment._bridge?.WeakEntity ?? WeakGameEntity.Invalid;
        var navmesh = attachment._navMeshBridge?.WeakEntity ?? WeakGameEntity.Invalid;
        return stepped == attachment.AttachmentSource.PlankBridgePhysicsEntity.WeakEntity
            || (bridge.IsValid && stepped.Root == bridge) || (navmesh.IsValid && stepped.Root == navmesh)
            || attachment.AttachmentSource.RampPhysicsList.Any(entity => entity.WeakEntity == stepped)
            || (attachment.AttachmentTarget != null && attachment.AttachmentTarget.RampPhysicsList.Any(entity => entity.WeakEntity == stepped));
    }

    public bool TryDescribeStation(UsableMissionObject point, out MissionObject hull, out string stationKey, out int pointIndex)
    {
        hull = null;
        stationKey = null;
        pointIndex = -1;
        var shipsLogic = ShipsLogic;
        // A dropped weapon lying on a deck is a pickup with its own sync, never a ship station.
        if (point == null || point is SpawnedItemEntity || shipsLogic == null || !point.GameEntity.IsValid) return false;

        var entity = point.GameEntity;
        for (int depth = 0; depth <= MaxPathDepth && entity.IsValid; depth++)
        {
            var ship = shipsLogic.AllShips.FirstOrDefault(candidate => candidate.GameEntity == entity);
            if (ship != null)
            {
                stationKey = EntityPath(point.GameEntity, ship.GameEntity);
                if (stationKey == null) return false;

                hull = ship;
                pointIndex = point.GameEntity.GetScriptComponents<UsableMissionObject>().ToList().IndexOf(point);
                return pointIndex >= 0;
            }

            entity = entity.Parent;
        }

        return false;
    }

    /// <summary>
    /// The entity's indexed, named child path below <paramref name="hullEntity"/>, or null outside it. Named child
    /// paths are content identities, never process-local native pointers.
    /// </summary>
    internal static string EntityPath(WeakGameEntity entity, WeakGameEntity hullEntity)
    {
        var parts = new List<string>();
        while (entity.IsValid && parts.Count <= MaxPathDepth)
        {
            if (entity == hullEntity)
            {
                parts.Reverse();
                return string.Join("/", parts);
            }

            var parent = entity.Parent;
            if (!parent.IsValid) return null;
            int index = parent.GetChildren().ToList().IndexOf(entity);
            if (index < 0) return null;
            parts.Add(index.ToString(CultureInfo.InvariantCulture) + ":" + entity.Name);
            entity = parent;
        }

        return null;
    }

    /// <summary>The entity at <paramref name="path"/> below <paramref name="hullEntity"/>; invalid when it is not there.</summary>
    internal static WeakGameEntity ResolvePath(WeakGameEntity hullEntity, string path)
    {
        var entity = hullEntity;
        if (!entity.IsValid || path == null) return WeakGameEntity.Invalid;

        foreach (var part in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = part.IndexOf(':');
            if (separator <= 0 || !int.TryParse(part.Substring(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                || index >= entity.ChildCount)
                return WeakGameEntity.Invalid;

            var child = entity.GetChild(index);
            if (!child.IsValid || child.Name != part.Substring(separator + 1)) return WeakGameEntity.Invalid;
            entity = child;
        }

        return entity;
    }

    public UsableMissionObject ResolveStation(MissionObject hull, string stationKey, int pointIndex)
    {
        if (pointIndex < 0) return null;

        var entity = ResolvePath(((MissionShip)hull).GameEntity, stationKey);
        var point = entity.IsValid ? entity.GetScriptComponents<UsableMissionObject>().Skip(pointIndex).FirstOrDefault() : null;
        // A dropped weapon on a deck is a pickup with its own sync, never a station.
        return point is SpawnedItemEntity ? null : point;
    }

    public void ApplyStationUse(Agent agent, UsableMissionObject point, bool inUse)
    {
        if (inUse)
        {
            // Agent.UseGameObject doesn't validate, so a removed point would bind the puppet to a freed object. A deactivated
            // machine stays usable: its state is recomputed locally and can lag the owner's.
            if (point is SpawnedItemEntity || !point.GameEntity.IsValid || IsSeated(agent, point)) return;
            if (agent.CurrentlyUsedGameObject != null) agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
            agent.UseGameObject(point);

            // A controller-less puppet never walks into the seat; seat it the way vanilla seats spawn crew: the helm
            // sets its steering action and pins the pilot, an oar sits its rower down at once.
            var machine = PilotMachineOf(point);
            if (machine != null && machine.PilotAgent == agent) machine.OnPilotAssignedDuringSpawn();
            return;
        }

        if (agent.CurrentlyUsedGameObject == point)
            agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
    }

    public bool IsAlive(Agent agent) => agent != null && agent.IsActive();

    public bool IsMainAgent(Agent agent) => agent != null && agent == Mission.Current?.MainAgent;

    public bool IsSeated(Agent agent, UsableMissionObject point) =>
        agent != null && point != null && agent.IsActive() && point.UserAgent == agent && agent.CurrentlyUsedGameObject == point;

    // Re-pinned every tick, because vanilla's seat lock alone lets a seated foreign actor slide.
    public void PinToStation(Agent agent, UsableMissionObject point)
    {
        if (!point.LockUserFrames) return;

        var frame = point.GetUserFrameForAgent(agent);
        agent.SetTargetPositionAndDirection(frame.Origin.AsVec2, in frame.Rotation.f);
    }

    public BattleRopeState[] CaptureRopes(MissionObject hull, Func<MissionObject, Guid> shipIdOf) =>
        NavalRopes.Capture((MissionShip)hull, shipIdOf);

    public void ApplyRopes(MissionObject hull, BattleRopeState[] ropes, Func<Guid, MissionObject> hullOf, bool final) =>
        NavalRopes.Apply((MissionShip)hull, ropes, hullOf, final);

    public object InspectRopes(IEnumerable<MissionObject> hulls, Func<MissionObject, Guid> shipIdOf) =>
        NavalRopes.Inspect(hulls.OfType<MissionShip>(), shipIdOf);

    public void ApplyShipDamage(MissionObject hull, NetworkApplyShipDamage damage, Agent attacker, MissionObject hitter)
    {
        var ship = (MissionShip)hull;
        if (!ship.GameEntity.IsValid || ship.IsDisabled) return;

        var point = ship.GlobalFrame.TransformToParent(damage.LocalPoint);
        using (NavalShipDamageGate.RoutedApply())
        {
            switch (damage.Kind)
            {
                case BattleShipDamageKind.Hull:
                    ship.DealDamage(damage.Damage, hitter as MissionShip, out _, out _, out _, out _);
                    return;
                case BattleShipDamageKind.Collision:
#if DEBUG
                    NavalContactProbe.CountRoutedCollisionApply();
#endif
                    ship.DealCollisionDamage(hitter as MissionShip, damage.IsRamDamage, point, damage.Damage);
                    return;
                case BattleShipDamageKind.Sails:
                    var sails = ship.Sails;
                    var sail = damage.SailIndex >= 0 && damage.SailIndex < sails.Count ? sails[damage.SailIndex] : null;
                    ship.DealDamageToSails(attacker, damage.Damage, damage.InflictedDamage, sail);
                    return;
                case BattleShipDamageKind.Fire:
                    ApplyFireDamage(ship, attacker, damage.Damage, point);
                    return;
            }
        }
    }

    public BattleShipCondition ReadCondition(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        return new BattleShipCondition(ship.HitPoints, ship.SailHitPoints, ship.FireHitPoints, ship._partialHitPoints?.ToArray(),
            (int)ship.Physics.NavalSinkingState);
    }

    // The reserve vanilla's IsSideDepleted counts per hull: queued troops of a hull that still spawns reinforcements.
    public int ReadSpawnableReserve(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var agentsLogic = Mission.Current?.GetMissionBehavior<NavalAgentsLogic>();
        if (agentsLogic == null || ship.Team == null || NavalForeignHulls.Contains(ship)) return 0;

        int active = agentsLogic.GetActiveAgentsOfShip(ship)?.Count ?? 0;
        return Math.Max(0, agentsLogic.GetTotalTroopCountOfShip(ship, spawnableReservesOnly: true) - active);
    }

    public BattleSideEnum SideOf(MissionObject hull) => ((MissionShip)hull).Team?.Side ?? BattleSideEnum.None;

    public int AddHullReserve(MissionObject hull, IReadOnlyList<IAgentOriginBase> origins)
    {
        var ship = (MissionShip)hull;
        var agentsLogic = Mission.Current?.GetMissionBehavior<NavalAgentsLogic>();
        if (agentsLogic == null || ship.Team == null || ship.IsSinking || ship.IsRemoved || NavalForeignHulls.Contains(ship)) return 0;

        // A taken-over hull never had its crew target set; vanilla's unbalanced AutoComputeDesiredTroopCountsPerShip uses the capacity.
        agentsLogic.SetDesiredTroopCountOfShip(ship, ship.TotalCrewCapacity);
        int added = 0;
        foreach (var origin in origins)
        {
            if (!agentsLogic.AddReservedTroopToShip(origin, ship)) break;
            added++;
        }

        // The NPC fleet spawner's crewing: the team spawns its hulls' reserve on reinforcement timers.
        var teamSide = ship.Team.TeamSide;
        if (added > 0 && !agentsLogic.GetSpawnReinforcementsOnTick(teamSide))
        {
            agentsLogic.InitializeReinforcementTimers(teamSide);
            agentsLogic.SetSpawnReinforcementsOnTick(teamSide, true, resetShips: false);
        }

        return added;
    }

    public string CampaignShipIdOf(MissionObject hull) => CampaignShipIdOf(((MissionShip)hull).ShipOrigin as Ship);

    public float ReadMitigatedDamage(MissionObject hull) => CoopShipSnapshots.MitigatedDamageOf(((MissionShip)hull).ShipOrigin as Ship);

    /// <summary>
    /// An own snapshot's party ship by its registry id, or the id another owner's record gave the snapshot; null for any other
    /// ship origin.
    /// </summary>
    internal string CampaignShipIdOf(Ship snapshot)
    {
        var source = CoopShipSnapshots.SourceOf(snapshot);
        if (source == null) return CoopShipSnapshots.CampaignShipIdOf(snapshot);
        return objectManager.TryGetId(source, out var id) ? id : null;
    }

    // NavalTeamAgents.OnShipRemoved fades a removed hull's crew this way.
    public void WithdrawAgent(Agent agent)
    {
        if (!agent.IsActive()) return;

        agent.SetIsPhysicsForceClosed(false);
        agent.FadeOut(true, true);
    }

    public bool IsSinking(MissionObject hull) => hull is MissionShip ship && ship.IsSinking;

    public bool IsHullRemoved(MissionObject hull) => hull is MissionShip ship && (ship.IsRemoved || !ship.GameEntity.IsValid);

    public void ApplyCondition(MissionObject hull, BattleShipCondition condition)
    {
        var ship = (MissionShip)hull;
        if (condition == null || !ship.GameEntity.IsValid) return;

        bool isCopy = NavalForeignHulls.Contains(ship);
        using (NavalShipDamageGate.RoutedApply())
        {
            if (ship.ShipOrigin is Ship origin)
            {
                origin.HitPoints = condition.HitPoints;
                origin.SailHitPoints = condition.SailHitPoints;
            }

            ApplyPartialHitPoints(ship, condition.PartialHitPoints);

            // At 0 sail HP DealDamageToSails sets the sails burning; zero damage changes no HP.
            if (ship.SailHitPoints <= 0f && ship.ShipSailState == MissionShip.SailState.Intact)
                ship.DealDamageToSails(null, 0f, 0f, null);

            bool catchesFire = condition.FireHitPoints <= 0f && ship.FireHitPoints > 0f;
            ship.FireHitPoints = condition.FireHitPoints;
            if (catchesFire)
            {
                ship.PrepareForAbandonment();
                ship.GameEntity.GetFirstScriptOfTypeRecursive<ShipBurningSystem>()?.StartFire();
                ship.ShipsLogic.OnShipBurned(ship);
            }

            ApplySinkingState(ship, (SinkingState)condition.SinkingState, isCopy);
        }

        // PrepareForAbandonment and SetSinkingState leave the hull an auto-updated controller; a copy keeps none.
        if (isCopy) ship.SetController(ShipControllerType.None, autoUpdateController: false);
    }

    private static void ApplyPartialHitPoints(MissionShip ship, float[] partialHitPoints)
    {
        var parts = ship._partialHitPoints;
        float max = ship.MaxPartialHealth;
        if (partialHitPoints == null || parts == null || max <= 0f) return;

        for (int i = 0; i < parts.Length && i < partialHitPoints.Length; i++)
        {
            parts[i] = partialHitPoints[i];
            ship.Physics.SetTargetDurabilityOfPart(i, partialHitPoints[i] / max);
        }
    }

    // SetSinkingState(Sinking) is what DealDamage does at 0 HP. Sunk is what OnTick does once the hull is under water,
    // which a copy reaches on its own when the owner's samples carry it down; this covers a copy that never does.
    private static void ApplySinkingState(MissionShip ship, SinkingState target, bool isCopy)
    {
        if (target != SinkingState.Floating && ship.Physics.NavalSinkingState == SinkingState.Floating)
            ship.SetSinkingState(SinkingState.Sinking);

        if (target != SinkingState.Sunk || !isCopy || ship.IsSunk) return;

        ship.SetSinkingState(SinkingState.Sunk);
        ship.ShipSailState = MissionShip.SailState.Destroyed;
        if (ship.SailBurningSoundEvent?.IsPlaying() == true) ship.SailBurningSoundEvent.Stop();
        ship.ShipsLogic.OnShipSunk(ship);
    }

    // MissionShip.OnHit's fire branch: a heavy hit leaves a burn mark, and at 0 fire HP the sails and hull burn and the
    // crew abandons ship.
    private static void ApplyFireDamage(MissionShip ship, Agent attacker, float fireDamage, Vec3 point)
    {
        if (ship.FireHitPoints <= 0f) return;

        float dealt = ship.DealFireDamage(fireDamage);
        var burning = ship.GameEntity.GetFirstScriptOfTypeRecursive<ShipBurningSystem>();
        if (dealt > 40f) burning?.RegisterBlow(point);
        if (ship.FireHitPoints > 0f) return;

        ship.DealDamageToSails(attacker, ship.SailHitPoints, ship.SailHitPoints, null);
        ship.PrepareForAbandonment();
        burning?.StartFire();
        ship.ShipsLogic.OnShipBurned(ship);
    }

    public bool TryGetWeapon(MissionObject hull, out MissionObject weapon, out string weaponKey)
    {
        weapon = null;
        weaponKey = null;
        var ship = (MissionShip)hull;
        var siegeWeapon = ship.ShipSiegeWeapon;
        if (siegeWeapon == null || !ship.GameEntity.IsValid || !siegeWeapon.GameEntity.IsValid) return false;

        weaponKey = EntityPath(siegeWeapon.GameEntity, ship.GameEntity);
        if (weaponKey == null) return false;

        weapon = siegeWeapon;
        return true;
    }

    public BattleShipWeaponState ReadWeaponState(MissionObject weapon)
    {
        var siegeWeapon = (RangedSiegeWeapon)weapon;
        return new BattleShipWeaponState(siegeWeapon.TargetDirection, siegeWeapon.TargetReleaseAngle, (int)siegeWeapon.State,
            siegeWeapon.AmmoCount);
    }

    // Never assigns State: its setter runs the simulator's side effects, ShootProjectile among them.
    public void ApplyWeaponState(MissionObject weapon, BattleShipWeaponState state, BattleShipWeaponState previous)
    {
        var siegeWeapon = (RangedSiegeWeapon)weapon;
        if (!siegeWeapon.GameEntity.IsValid) return;

        // The HandleUserAiming prefix re-asserts the targets each tick, so vanilla's speed-limited approach turns the weapon.
        SiegeMissionAuthorityGate.SetRemoteAim(siegeWeapon.Id.Id, state.Direction, state.ReleaseAngle);
        siegeWeapon.SetAmmo(state.AmmoCount);
        replicatedWeaponStates[weapon] = state;
        if (previous != null && previous.WeaponState == state.WeaponState) return;

        var weaponState = (RangedSiegeWeapon.WeaponState)state.WeaponState;
        SiegeMachineStateReplicator.PlayWeaponStateAnimation(siegeWeapon, weaponState);
        ShowLoadedProjectile(siegeWeapon,
            weaponState == RangedSiegeWeapon.WeaponState.Idle || weaponState == RangedSiegeWeapon.WeaponState.WaitingBeforeIdle);
    }

    public bool ReplayWeaponShot(MissionObject weapon, NetworkShipWeaponShot shot, Agent shooter, bool playLaunchEffects)
    {
        var siegeWeapon = (RangedSiegeWeapon)weapon;
        var mission = Mission.Current;
        if (mission == null || !siegeWeapon.GameEntity.IsValid) return false;

        shooter = shooter ?? SiegeWeaponFireReplicator.FindStandInShooter();
        var missileItem = MBObjectManager.Instance.GetObject<ItemObject>(shot.MissileItemId);
        if (shooter == null || missileItem == null) return false;

        // Not added as a ship siege missile, so not even an ungated OnHit would deal hull damage from it.
        var ignored = siegeWeapon.GameEntity.Root.GetFirstScriptOfType<MissionObject>() ?? siegeWeapon;
        mission.AddCustomMissile(shooter, new MissionWeapon(missileItem, null, null, 1), shot.Position, shot.Direction,
            shot.Orientation, shot.BaseSpeed, shot.Speed, addRigidBody: false, ignored);
        if (!playLaunchEffects) return true;

        SiegeWeaponFireReplicator.PlayFireAnimation(siegeWeapon);
        ShowLoadedProjectile(siegeWeapon, false);
        if (siegeWeapon.FireSoundIndex >= 0)
            SoundManager.StartOneShotEventWithIndex(siegeWeapon.FireSoundIndex, siegeWeapon.GameEntity.GetGlobalFrame().origin);
        return true;
    }

    // Vanilla shows and hides the loaded projectile only on the simulating client.
    private static void ShowLoadedProjectile(RangedSiegeWeapon siegeWeapon, bool visible)
    {
        var projectile = siegeWeapon.Projectile;
        if (projectile != null && projectile.GameEntity.IsValid) projectile.GameEntity.SetVisibilityExcludeParents(visible);
    }

    public void SetSimulatedWeapons(IReadOnlyCollection<MissionObject> own)
    {
        // A weapon simulated here reads its own State again, so a swap must not carry an older authority's state.
        foreach (var weapon in own) replicatedWeaponStates.Remove(weapon);
        // No host-wide default at sea: a ship weapon not claimed here belongs to its hull's owner.
        SiegeMissionAuthorityGate.IsLocalAuthority = false;
        SiegeMissionAuthorityGate.SetClaimedMachines(new HashSet<int>(own.Select(weapon => weapon.Id.Id)), new HashSet<int>());
        SiegeMissionAuthorityGate.IsAuthorityKnown = true;
    }

    public void ClearSimulatedWeapons()
    {
        replicatedWeaponStates.Clear();
        SiegeMissionAuthorityGate.IsAuthorityKnown = false;
        SiegeMissionAuthorityGate.ResetClaimedMachines();
    }

    // The machine whose pilot point this is; the point entity sits at most a few levels below its machine.
    private static UsableMachine PilotMachineOf(UsableMissionObject point)
    {
        var entity = point.GameEntity;
        for (int depth = 0; depth < 4 && entity.IsValid; depth++)
        {
            var machine = entity.GetScriptComponents<UsableMachine>().FirstOrDefault(candidate => candidate.PilotStandingPoint == point);
            if (machine != null) return machine;
            entity = entity.Parent;
        }

        return null;
    }
}

/// <summary>How vanilla's removal can take a hull (<see cref="NavalShipEngine.RemovalPathOf"/>).</summary>
internal enum HullRemovalPath
{
    /// <summary>An assignment holds the hull, so vanilla's RemoveShip finds it.</summary>
    Assigned,

    /// <summary>No assignment holds the hull's origin (a capture's orphan), so it borrows a free one.</summary>
    Unassigned,

    /// <summary>Another hull's assignment holds the same origin, so vanilla would remove that hull instead.</summary>
    OriginHeldByAnotherHull,
}

/// <summary>What a removal through a lent assignment did (<see cref="NavalShipEngine.RemoveThroughLentAssignment"/>).</summary>
internal enum LentRemoval
{
    Removed,
    NoFreeAssignment,
    NotRemoved,
}
