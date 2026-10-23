using Common.Logging;
using Common.Messaging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.ViewModelCollection.OrderOfBattle;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using NavalPhysicsScript = NavalDLC.Missions.NavalPhysics.NavalPhysics;

namespace Missions.Naval;

// Every NavalShipsLogic spawn funnels through the formation overload; announce it so the replicator can register own hulls.
[HarmonyPatch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.SpawnShip),
    new[] { typeof(Formation), typeof(MatrixFrame), typeof(bool), typeof(bool) },
    new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalShipSpawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(MissionShip __result, Formation formation)
    {
        if (__result == null) return;

        MessageBroker.Instance.Publish(__result, new ShipSpawnedInBattle(__result, formation));
    }
}

// MissionShip.OnDeploymentFinished only runs on hulls spawned before deployment ends, so a later hull (AI fleet, foreign copy,
// hull swap) gets the weapon, helm and crew steps here. Ammo and anchor stay as spawned, so a swap doesn't refill ammo.
[HarmonyPatch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.SpawnShip),
    new[] { typeof(Formation), typeof(MatrixFrame), typeof(bool), typeof(bool) },
    new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class LateHullDeploymentPatch
{
    // Runs the deployment steps a late hull needs; each one is safe to repeat on a pooled hull that vanilla deployed before.
    [HarmonyPostfix]
    private static void Postfix(NavalShipsLogic __instance, MissionShip __result)
    {
        if (__result == null) return;
        if (!FinishesDeployment(CoopNavalMissionScope.IsActive, Mission.Current?.IsDeploymentFinished == true)) return;

        // A ballista lists its ship targets once, at its own deployment (ThreatSeeker.InitializeTargetableObjects), so in vanilla
        // every hull is in every list; here hulls arrive after deployment, so the hulls already fielded learn of this one.
        RefreshShipWeaponTargets(__instance.AllShips, __result);

        // Readies the ballista AI's threat seeker, otherwise ThreatSeeker.GetAllThreats throws on its first tick.
        __result.ShipSiegeWeapon?.OnDeploymentFinished();
        // Adds the helm's standing point components, makes an enemy helm AI-only and sets the logic its vacancy check reads.
        __result.ShipControllerMachine.OnDeploymentFinished();
        // The first unit's party feeds a crew perk; a fresh AI fleet or copy formation has no unit yet, so that perk is left out.
        __result.CrewSizeOnMainDeck = MissionGameModels.Current.MissionShipParametersModel
            .CalculateMainDeckCrewSize(__result.ShipOrigin, __result.Formation.GetFirstUnit());
        // Checked from the mission tick, not here: the async team tick may be reordering this formation's behaviours.
        NavalFormationTacticRefresh.Note(__result.Formation);
    }

    /// <summary>Re-reads the targetable mission objects of every fielded hull's ballista but the new hull's (deployed above).</summary>
    internal static void RefreshShipWeaponTargets(IEnumerable<MissionShip> ships, MissionShip spawned)
    {
        foreach (var ai in WeaponAisToRefresh(ships, spawned))
            ai.InitializeThreatSeeker();
    }

    /// <summary>The ranged weapon AIs of the hulls still in the mission, the newly spawned one excepted.</summary>
    internal static IEnumerable<RangedSiegeWeaponAi> WeaponAisToRefresh(IEnumerable<MissionShip> ships, MissionShip spawned) =>
        ships.Where(ship => ship != null && ship != spawned && !ship.IsRemoved)
            .Select(ship => ship.ShipSiegeWeapon?.Ai as RangedSiegeWeaponAi)
            .Where(ai => ai != null);

    // Hulls spawned before deployment ends get it from vanilla's own deployment pass.
    internal static bool FinishesDeployment(bool coopNavalBattle, bool deploymentFinished) => coopNavalBattle && deploymentFinished;
}

// Only the owner runs a hull's ship order; a foreign copy follows the owner's frames.
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.Tick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignShipOrderTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipOrder __instance) => !NavalForeignHulls.Contains(__instance._ownerShip);
}

// ShipRetreatLogic removes a hull whose order is Retreat once it reaches the map edge; only the owner may retreat its hull.
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.SetShipRetreatOrder))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignShipRetreatOrderPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipOrder __instance) => !NavalForeignHulls.Contains(__instance._ownerShip);
}

// Detachments move crew onto machines; on a foreign copy that is the owner's decision.
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.ManageShipDetachments))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignShipDetachmentsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ShipOrder __instance) => !NavalForeignHulls.Contains(__instance._ownerShip);
}

// Vanilla hands every non-player hull to AI when deployment ends; foreign copies stay uncontrolled.
[HarmonyPatch(typeof(DefaultNavalMissionLogic), nameof(DefaultNavalMissionLogic.OnDeploymentFinished))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignHullControllerPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        foreach (var ship in NavalForeignHulls.All)
            ship.SetController(ShipControllerType.None, autoUpdateController: false);
    }
}

// The native contact filter asks every script on both bodies. Nobody here simulates either of two copies, so their contact
// is noise this client can't own; each owner resolves its own hull against the other's copy.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.CanPhysicsCollideBetweenTwoEntities))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopyPairCollisionFilterPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MissionShip __instance, WeakGameEntity otherEntity, ref bool __result)
    {
        // The root scan only runs for a copy, since an own hull touches anything.
        bool mineIsCopy = NavalForeignHulls.Contains(__instance);
        if (AllowsCollision(mineIsCopy, mineIsCopy && NavalForeignHulls.ContainsRootOf(otherEntity))) return true;

#if DEBUG
        NavalContactProbe.CountFilteredCopyPair();
#endif
        __result = false;
        return false;
    }

    /// <summary>Whether two hulls may touch on this client: not when both are copies of other owners' hulls.</summary>
    internal static bool AllowsCollision(bool mineIsCopy, bool otherIsCopy) => !(mineIsCopy && otherIsCopy);
}

// Vanilla weighs a hull down with the agents standing on it. A copy's puppets stand and sit elsewhere than the owner's crew,
// which heeled copies 2.5-4.2 degrees off (R45), so a copy takes the owner's crew load from its samples instead.
[HarmonyPatch(typeof(NavalPhysicsScript), nameof(NavalPhysicsScript.ApplyAgentForces))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopyCrewLoadPatch
{
    // The tolerance vanilla's ApplyAgentForces gives its point-in-hull check.
    internal const float BoxEpsilon = 0.1f;

    // [Fixed tick] Applies the owner's crew load on a copy that has one, none for an empty deck; an own hull, or a copy
    // without a load yet, runs vanilla.
    [HarmonyPrefix]
    private static bool Prefix(NavalPhysicsScript __instance)
    {
        bool isCopy = NavalForeignHulls.TryGetCopy(__instance, out var copy);
        var ownerLoad = copy?.CrewLoad;
        if (!AppliesOwnerLoad(isCopy, ownerLoad)) return true;
        if (ownerLoad.IsEmpty) return false;

        var localCentre = ownerLoad.LocalCentre;
        if (!IsInsideHull(__instance.PhysicsBoundingBoxWithoutChildren, localCentre)) return false;

        var force = WeightForce(ownerLoad.Mass, __instance.PhysicsParameters.StepAgentWeightMultiplier);
        __instance.ApplyGlobalForceAtLocalPos(in localCentre, in force);
        return false;
    }

    /// <summary>Whether a hull takes its owner's crew load: only a copy whose owner sent one.</summary>
    internal static bool AppliesOwnerLoad(bool isCopy, BattleShipCrewLoad ownerLoad) => isCopy && ownerLoad != null;

    /// <summary>Vanilla's crew weight force: the mass times the hull's step weight multiplier times gravity.</summary>
    internal static Vec3 WeightForce(float mass, float stepAgentWeightMultiplier) =>
        mass * stepAgentWeightMultiplier * MBGlobals.GravitationalAcceleration;

    /// <summary>Vanilla's check that the crew centre lies within the hull's physics box, which it skips the force without.</summary>
    internal static bool IsInsideHull(BoundingBox box, Vec3 localCentre) => box.PointInsideBox(localCentre, BoxEpsilon);
}

// Marks the initial plan of a side so the plan patch below shifts only that plan, not later Order of Battle replans.
[HarmonyPatch(typeof(DefaultNavalMissionLogic), nameof(DefaultNavalMissionLogic.MakeDeploymentPlansForSide))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalSidePlanScopePatch
{
    [HarmonyPrefix]
    private static void Prefix(DefaultNavalMissionLogic __instance) => NavalPlayerDeploymentSlot.PlanningSide = __instance;

    [HarmonyFinalizer]
    private static void Finalizer() => NavalPlayerDeploymentSlot.PlanningSide = null;
}

// Replans keep the stored offset, so shifting the initial plan moves this client's fleet for the whole deployment.
[HarmonyPatch(typeof(NavalMissionDeploymentPlanningLogic), nameof(NavalMissionDeploymentPlanningLogic.MakeDeploymentPlan))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class PlayerFleetDeploymentSlotPatch
{
    [HarmonyPrefix]
    private static void Prefix(Team team, ref float spawnPathOffset)
    {
        var planningSide = NavalPlayerDeploymentSlot.PlanningSide;
        int rank = NavalPlayerDeploymentSlot.Rank;
        var mission = Mission.Current;
        if (planningSide == null || rank <= 0 || mission == null || team != mission.PlayerTeam) return;

        float teamRange = planningSide.GetTeamSpawnPathOffsetRange(mission.GetInitialSpawnPathData(team.Side).Path, team);
        spawnPathOffset = NavalPlayerDeploymentSlot.ShiftSpawnPathOffset(spawnPathOffset, rank, teamRange);
    }
}

// The order of battle restores its saved ship layout by Ship reference; a coop battle fields fresh snapshots that never
// match, so it would empty every formation whose ship can move and remove all own hulls but the flagship at Ready.
[HarmonyPatch(typeof(NavalOrderOfBattleVM), nameof(NavalOrderOfBattleVM.LoadConfigurationShips))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopOrderOfBattleShipLayoutPatch
{
    [HarmonyPrefix]
    internal static bool Prefix() => !CoopNavalMissionScope.IsActive;
}

// Owners announce station use; the replicator ignores agents this client does not drive. An item pickup is no station
// (it has its own sync) and runs on a parallel tick worker, so it is never published.
[HarmonyPatch(typeof(Agent), nameof(Agent.UseGameObject))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class AgentStationUsePatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __instance, UsableMissionObject usedObject)
    {
        if (!CoopNavalMissionScope.IsActive || usedObject == null || usedObject is SpawnedItemEntity) return;

        MessageBroker.Instance.Publish(__instance, new AgentStationUseChanged(__instance, usedObject, inUse: true));
    }
}

// Every release path (death, order, AI, player) ends in StopUsingGameObjectAux.
[HarmonyPatch(typeof(Agent), nameof(Agent.StopUsingGameObjectAux))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class AgentStationReleasePatch
{
    [HarmonyPrefix]
    private static void Prefix(Agent __instance, out UsableMissionObject __state) => __state = __instance.CurrentlyUsedGameObject;

    [HarmonyPostfix]
    private static void Postfix(Agent __instance, UsableMissionObject __state)
    {
        if (!CoopNavalMissionScope.IsActive || __state == null || __state is SpawnedItemEntity
            || __instance.CurrentlyUsedGameObject == __state)
            return;

        MessageBroker.Instance.Publish(__instance, new AgentStationUseChanged(__instance, __state, inUse: false));
    }
}

// A copy is steered by its owner, so the player takes another owner's helm only to capture an enemy hull; vanilla's helm
// gates that further (AI-only until the formation empties, disabled until vacant) and its timer asks the hull's authority.
[HarmonyPatch(typeof(Agent), nameof(Agent.HandleStartUsingAction))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignHelmUsePatch
{
    // Refuses the main agent's start at a helm it may not take in a co-op battle; any other use goes through.
    [HarmonyPrefix]
    private static bool Prefix(Agent __instance, UsableMissionObject targetObject)
    {
        if (!__instance.IsMainAgent || !CoopNavalMissionScope.IsActive) return true;

        var hull = HelmHullOf(targetObject);
        if (hull == null) return true;

        return AllowsStart(NavalForeignHulls.Contains(hull), IsEnemyHull(hull, __instance));
    }

    /// <summary>
    /// Whether the main agent may start at a hull's helm: any hull it simulates, where vanilla's own rules apply, and another
    /// owner's copy only when that copy's team is an enemy, to capture it. A capture's orphan has no team, so others keep off it.
    /// </summary>
    internal static bool AllowsStart(bool isForeignHelm, bool isEnemyHull) => !isForeignHelm || isEnemyHull;

    // The hull whose helm owns this standing point, or null when it is no helm.
    private static MissionShip HelmHullOf(UsableMissionObject point)
    {
        var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
        if (point is not StandingPoint standingPoint || shipsLogic == null) return null;

        foreach (var ship in shipsLogic.AllShips)
        {
            var helm = ship.ShipControllerMachine;
            if (helm != null && (helm.PilotStandingPoint == standingPoint || helm.StandingPoints.Contains(standingPoint)))
                return ship;
        }

        return null;
    }

    // A team that is an enemy of the agent's; an orphan's missing team is none, unlike vanilla's side test, which reads it as -1.
    private static bool IsEnemyHull(MissionShip hull, Agent agent) => agent.Team != null && hull.Team != null && hull.Team.IsEnemyOf(agent.Team);
}

// Vanilla's capture swaps two hulls' formations, sides and controllers on this client alone. In a co-op battle the elapsed timer
// goes to the capture replicator instead, and the hull's authority decides and broadcasts the capture every client replays.
[HarmonyPatch(typeof(ShipControllerMachine), nameof(ShipControllerMachine.OnShipCapturedByAgent))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopShipCapturePatch
{
    // Skips NavalShipsLogic.OnShipCaptured in a co-op battle; the elapsed timer has already taken the captor off the helm.
    [HarmonyPrefix]
    private static bool Prefix(ShipControllerMachine __instance, Agent captorAgent)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        MessageBroker.Instance.Publish(__instance, new ShipCaptureTimerElapsed(__instance.AttachedShip, captorAgent));
        return false;
    }
}

// A captured player's formation keeps its main agent but its assignment no longer has a hull, so the helm's capture checks in
// ShipControllerMachine.OnTick ask whether a null hull is connected to the orphan every tick; vanilla never captures a player hull.
[HarmonyPatch(typeof(MissionShip), nameof(MissionShip.AreShipsConnected))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CapturedFormationConnectionPatch
{
    // Answers "not connected" for a missing hull in a co-op battle instead of dereferencing it.
    [HarmonyPrefix]
    internal static bool Prefix(MissionShip ship1, MissionShip ship2, ref bool __result)
    {
        if (!CoopNavalMissionScope.IsActive || (ship1 != null && ship2 != null)) return true;

        __result = false;
        return false;
    }
}

// The host plans the AI fleet's side after deployment ended; the deployment views (boundary markers) that listen
// for plans are done by then and throw, and nothing else listens, so that one late plan is not announced.
[HarmonyPatch(typeof(Mission), nameof(Mission.OnDeploymentPlanMade))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class LateDeploymentPlanNotificationPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !NavalLateDeploymentPlan.IsMaking;
}

/// <summary>[Game thread] Makes a battle side's deployment plan after deployment, without notifying mission listeners.</summary>
internal static class NavalLateDeploymentPlan
{
    internal static bool IsMaking { get; private set; }

    internal static void MakeForSide(DefaultNavalMissionLogic navalLogic, BattleSideEnum side)
    {
        IsMaking = true;
        try
        {
            navalLogic.MakeDeploymentPlansForSide(side);
        }
        finally
        {
            IsMaking = false;
        }
    }
}
