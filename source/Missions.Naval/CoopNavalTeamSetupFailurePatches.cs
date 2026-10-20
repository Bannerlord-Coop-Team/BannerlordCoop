using Common.Logging;
using HarmonyLib;
using NavalDLC.Missions.Deployment;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// A throw in a naval team setup leaves <c>TeamSetupOver</c> unset, so vanilla re-runs it every tick and spawns a hull per
/// formation each time until the game dies natively (R41a-2 S); the first failure ends the mission and setup never reruns.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalTeamSetupFailurePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalTeamSetupFailurePatches>();

    // Records a naval setup failure on the reserve guard, which ends the mission; the exception still reaches the tick's log.
    [HarmonyPatch(typeof(DeploymentMissionController), nameof(DeploymentMissionController.SetupTeams))]
    [HarmonyFinalizer]
    private static Exception SetupTeamsFinalizer(DeploymentMissionController __instance, Exception __exception)
    {
        if (__exception == null || __instance is not NavalDeploymentMissionController) return __exception;

        var guard = __instance.Mission?.GetMissionBehavior<CoopNavalReserveGuard>();
        if (guard == null)
        {
            Logger.Error("[NavalBattle] Naval team setup failed and no reserve guard ends the mission; vanilla will retry it");
            return __exception;
        }

        if (guard.FailSetup())
            Logger.Error(__exception, "[NavalBattle] Naval team setup failed; ending the mission instead of retrying it every tick");
        return __exception;
    }

    // Skips the deployment controller's tick once its naval team setup failed, so the side deployment never runs twice.
    [HarmonyPatch(typeof(DeploymentMissionController), nameof(DeploymentMissionController.OnMissionTick))]
    [HarmonyPrefix]
    private static bool OnMissionTickPrefix(DeploymentMissionController __instance)
    {
        if (!CoopNavalMissionScope.IsActive || __instance is not NavalDeploymentMissionController) return true;

        return RunsDeploymentTick(__instance.Mission?.GetMissionBehavior<CoopNavalReserveGuard>()?.SetupFailed == true);
    }

    // Whether the deployment controller may tick: never again after its naval team setup failed.
    internal static bool RunsDeploymentTick(bool setupFailed) => !setupFailed;
}
