using GameInterface.Services.ObjectManager;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
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

namespace Missions.Naval;

/// <inheritdoc cref="INavalShipEngine"/>
public class NavalShipEngine : INavalShipEngine
{
    private const int MaxPathDepth = 16;

    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;

    public NavalShipEngine(ICoopShipSnapshotBuilder snapshotBuilder, IObjectManager objectManager)
    {
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
    }

    private static NavalShipsLogic ShipsLogic => Mission.Current?.GetMissionBehavior<NavalShipsLogic>();

    public IReadOnlyList<MissionObject> Hulls =>
        ShipsLogic?.AllShips.Cast<MissionObject>().ToList() ?? new List<MissionObject>();

    public BattleShipSpawnData Describe(MissionObject hull, NetworkShipInfo identity)
    {
        var ship = (MissionShip)hull;
        var campaignShip = ship.ShipOrigin as Ship;
        var pieces = campaignShip?._shipPieces.Where(piece => piece.Value != null).ToArray()
            ?? Array.Empty<KeyValuePair<string, ShipUpgradePiece>>();

        return new BattleShipSpawnData(
            identity.ShipId,
            identity.CurrentAuthority,
            identity.MapEventPartyId,
            identity.IsNpcParty,
            ship.Team.Side,
            (int)ship.Formation.FormationIndex,
            NetworkBattleShipSample.FromFrame(ship.GlobalFrame),
            ship.ShipOrigin.Hull.StringId,
            pieces.Select(piece => piece.Key).ToArray(),
            pieces.Select(piece => piece.Value.StringId).ToArray(),
            campaignShip?.Figurehead?.StringId,
            campaignShip?._name?.ToString(),
            ship.ShipOrigin.HitPoints,
            ship.ShipOrigin.SailHitPoints,
            ship.ShipOrigin.RandomValue,
            ship.ShipOrigin.CustomSailPatternId);
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

        // The owner simulates this hull: no local controller, no auto controller, a kinematic body.
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
        ship.GameEntity.DisableDynamicBodySimulation();
        NavalForeignHulls.Add(ship);
        formation = ship.Formation;
        return ship;
    }

    public MissionObject SetNpcHullAuthority(MissionObject hull, bool owned)
    {
        var ship = (MissionShip)hull;
        if (owned) return ReplaceCopyWithSimulatedHull(ship);

        NavalForeignHulls.Add(ship);
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
        if (ship.GameEntity.IsValid) ship.GameEntity.DisableDynamicBodySimulation();
        return ship;
    }

    // EnableDynamicBody does not undo DisableDynamicBodySimulation (live: the body stayed inactive), and the engine has
    // no other inverse, so the copy is swapped for a hull spawned the way the host spawned it.
    private static MissionShip ReplaceCopyWithSimulatedHull(MissionShip copy)
    {
        var mission = Mission.Current;
        var shipsLogic = ShipsLogic;
        var agentsLogic = mission?.GetMissionBehavior<NavalAgentsLogic>();
        var formation = copy.Formation;
        var team = copy.Team;
        if (shipsLogic == null || agentsLogic == null || formation == null || team == null || !copy.GameEntity.IsValid)
            return copy;

        var origin = copy.ShipOrigin;
        var frame = copy.GlobalFrame;
        var crew = mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman && agent.Formation == formation).ToList();

        // Nobody may stay on a machine of the hull that is about to be removed, nor be tracked as its crew.
        foreach (var agent in mission.Agents)
        {
            if (agent.IsActive() && agent.CurrentlyUsedGameObject is UsableMissionObject used
                && used.GameEntity.IsValid && used.GameEntity.Root == copy.GameEntity)
                agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
        }

        foreach (var agent in crew)
        {
            if (agentsLogic.IsAgentOnAnyShip(agent, out var onShip) && onShip == copy)
                agentsLogic.RemoveAgentFromShip(agent, copy);
        }

        NavalForeignHulls.Remove(copy);
        shipsLogic.RemoveShip(copy);

        var fresh = shipsLogic.SpawnShip(origin, in frame, team, formation, spawnAnchored: false, checkForFreeArea: false);
        fresh.SetController(ShipControllerType.AI);
        formation.SetControlledByAI(true);

        // The adopted crew become the new hull's crew; vanilla seats the helm and oars and moves them onto its deck.
        foreach (var agent in crew)
        {
            if (agent.IsActive()) agentsLogic.AddAgentToShip(agent, fresh);
        }

        agentsLogic.AssignAndTeleportCrewToShipMachines(fresh);

        // Removing one hull and adding one leaves the planner's agent count unchanged, so its k-d tree keeps the removed
        // copy and never sees the new hull. Rebuild the planner the way vanilla does after changing ships mid-mission.
        mission.GetMissionBehavior<NavalTrajectoryPlanningLogic>()?.ForceReinitialize();
        return fresh;
    }

    public bool HasHelmPilot(MissionObject hull) => ((MissionShip)hull).ShipControllerMachine?.PilotAgent != null;

    public MatrixFrame GetFrame(MissionObject hull) => ((MissionShip)hull).GlobalFrame;

    public void ApplyForeignFrame(MissionObject hull, MatrixFrame frame)
    {
        var entity = ((MissionShip)hull).GameEntity;
        if (!entity.IsValid) return;

        entity.SetGlobalFrame(frame, isTeleportation: false);
        entity.UpdateAttachedNavigationMeshFaces();
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

    public bool IsBodyActive(MissionObject hull)
    {
        var entity = ((MissionShip)hull).GameEntity;
        return entity.IsValid && entity.HasDynamicRigidBodyAndActiveSimulation();
    }

    public string ControllerName(MissionObject hull) =>
        ((MissionShip)hull).Controller?.ControllerType.ToString() ?? ShipControllerType.None.ToString();

    public MissionObject GetSupportHull(Agent agent)
    {
        if (agent == null || !agent.IsActive() || agent.HasMount) return null;

        var stepped = agent.GetComponent<AgentNavalComponent>()?.SteppedShip;
        if (stepped != null && stepped.GetIsAgentOnShip(agent)) return stepped;

        return PlankSourceHull(agent);
    }

    // A connected plank belongs to no hull; its rope's source hull is the reference frame, as in the naval lab.
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
        if (point == null || shipsLogic == null || !point.GameEntity.IsValid) return false;

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
        return entity.IsValid ? entity.GetScriptComponents<UsableMissionObject>().Skip(pointIndex).FirstOrDefault() : null;
    }

    public void ApplyStationUse(Agent agent, UsableMissionObject point, bool inUse)
    {
        if (inUse)
        {
            if (IsSeated(agent, point)) return;
            if (agent.CurrentlyUsedGameObject != null) agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
            agent.UseGameObject(point);

            // A controller-less puppet never walks into the seat; seat it the way vanilla seats spawn crew (as the lab
            // did): the helm sets its steering action and pins the pilot, an oar sits its rower down at once.
            var machine = PilotMachineOf(point);
            if (machine != null && machine.PilotAgent == agent) machine.OnPilotAssignedDuringSpawn();
            return;
        }

        if (agent.CurrentlyUsedGameObject == point)
            agent.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
    }

    public bool IsAlive(Agent agent) => agent != null && agent.IsActive();

    public bool IsSeated(Agent agent, UsableMissionObject point) =>
        agent != null && point != null && agent.IsActive() && point.UserAgent == agent && agent.CurrentlyUsedGameObject == point;

    // The lab re-pinned every seated foreign actor on each hull frame write; vanilla's seat lock alone lets it slide.
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
