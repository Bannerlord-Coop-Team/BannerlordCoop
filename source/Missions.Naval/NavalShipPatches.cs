using Common.Messaging;
using HarmonyLib;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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

// Only the owner runs a hull's ship order; a foreign copy follows the owner's frames.
[HarmonyPatch(typeof(ShipOrder), nameof(ShipOrder.Tick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignShipOrderTickPatch
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

// Owners announce station use; the replicator ignores agents this client does not drive.
[HarmonyPatch(typeof(Agent), nameof(Agent.UseGameObject))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class AgentStationUsePatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __instance, UsableMissionObject usedObject)
    {
        if (!CoopNavalMissionScope.IsActive || usedObject == null) return;

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
        if (!CoopNavalMissionScope.IsActive || __state == null || __instance.CurrentlyUsedGameObject == __state) return;

        MessageBroker.Instance.Publish(__instance, new AgentStationUseChanged(__instance, __state, inUse: false));
    }
}

// The player can only take the helm of a hull it owns; a copied hull is steered by its owner.
[HarmonyPatch(typeof(Agent), nameof(Agent.HandleStartUsingAction))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class ForeignHelmUsePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Agent __instance, UsableMissionObject targetObject) =>
        AllowsStart(__instance.IsMainAgent, IsForeignHelm(targetObject));

    internal static bool AllowsStart(bool isMainAgent, bool isForeignHelm) => !isMainAgent || !isForeignHelm;

    private static bool IsForeignHelm(UsableMissionObject point)
    {
        if (point is not StandingPoint standingPoint) return false;

        foreach (var ship in NavalForeignHulls.All)
        {
            var helm = ship.ShipControllerMachine;
            if (helm != null && (helm.PilotStandingPoint == standingPoint || helm.StandingPoints.Contains(standingPoint)))
                return true;
        }

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
