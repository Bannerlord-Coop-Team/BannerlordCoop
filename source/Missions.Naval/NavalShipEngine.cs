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
using SinkingState = NavalDLC.Missions.NavalPhysics.NavalPhysics.SinkingState;

namespace Missions.Naval;

/// <inheritdoc cref="INavalShipEngine"/>
public class NavalShipEngine : INavalShipEngine
{
    private const int MaxPathDepth = 16;

    // Clear of every hull and the water, where NavalTeamAgents.UnassignAgentAux parks troops it takes off a ship.
    private const float ParkingHeight = 500f;

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

    public bool IsDeploymentMode => ShipsLogic?.IsDeploymentMode == true;

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

    public void ReleaseNpcHull(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        NavalForeignHulls.Add(ship);
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
        if (ship.GameEntity.IsValid) ship.GameEntity.DisableDynamicBodySimulation();
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
            bool isCrew = isTracked || (ship.Formation != null && agent.Formation == ship.Formation);
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

    private static bool UsesMachineOf(Agent agent, MissionShip ship) =>
        agent.CurrentlyUsedGameObject is UsableMissionObject used && used.GameEntity.IsValid && used.GameEntity.Root == ship.GameEntity;

    // EnableDynamicBody does not undo DisableDynamicBodySimulation (live: the body stayed inactive), and the engine has
    // no other inverse, so the hull is swapped for one spawned the way the host spawned it.
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

        // Quest5's RemoveShipInternal: no rope or bridge may keep pointing at a removed hull.
        ship.BreakAllExistingConnections();
        NavalForeignHulls.Remove(ship);
        shipsLogic.RemoveShip(ship);

        var fresh = shipsLogic.SpawnShip(origin, in frame, team, formation, spawnAnchored: false, checkForFreeArea: false);
        fresh.SetController(ShipControllerType.AI);
        formation.SetControlledByAI(true);

        // HP lives on the shared origin; fire, partial HP and sinking live on the hull, so a sinking hull keeps sinking.
        ApplyCondition(fresh, condition);

        // Removing one hull and adding one leaves the planner's agent count unchanged, so its k-d tree keeps the removed
        // hull and never sees the new one. Rebuild it the way vanilla does after changing ships mid-mission.
        mission.GetMissionBehavior<NavalTrajectoryPlanningLogic>()?.ForceReinitialize();
        return fresh;
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

    public bool IsSeated(Agent agent, UsableMissionObject point) =>
        agent != null && point != null && agent.IsActive() && point.UserAgent == agent && agent.CurrentlyUsedGameObject == point;

    // Re-pinned on each hull frame write, because vanilla's seat lock alone lets a seated foreign actor slide.
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

    public bool IsSinking(MissionObject hull) => hull is MissionShip ship && ship.IsSinking;

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
