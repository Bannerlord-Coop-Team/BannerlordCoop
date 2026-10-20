using Common.Logging;
using HarmonyLib;
using NavalDLC;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Naval team setups vanilla would retry every tick end the mission through <see cref="CoopNavalSetupGuard"/> instead: a
/// player team it can't allocate (R41a, R41a-2 S) and a setup that threw, which would spawn a hull per formation each time.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalTeamSetupFailurePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalTeamSetupFailurePatches>();

    // Runs the player team's initial allocation only with a ship and a player origin to put on it; it would throw every tick
    // otherwise. The mission ends, as in vanilla where a wounded hero can't enter a battle.
    [HarmonyPatch(typeof(NavalTeamSideSpawnContext), nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam))]
    [HarmonyPrefix]
    private static bool AllocationPrefix(NavalTeamSideSpawnContext __instance)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        bool hasShip = __instance._shipsLogic.GetShipAssignment(TeamSideEnum.PlayerTeam, FormationClass.Infantry)?.HasMissionShip == true;
        bool hasPlayerOrigin = __instance._agentsLogic.FindTroopOrigin(__instance.TeamSide, origin => origin.Troop.IsPlayerCharacter) != null;
        if (hasShip && hasPlayerOrigin) return true;

        string outcome = $"player team allocation skipped (ship={hasShip} hero={hasPlayerOrigin}), the mission keeps running";
        if (CoopNavalSetupGuard.TryEnd(Mission.Current, NavalSetupEndReason.PlayerTeamNotAllocatable, outcome))
            Logger.Warning("[NavalBattle] The player's team can't be allocated (ship={HasShip} hero={HasHero}); skipping its initial troop allocation and ending the mission",
                hasShip, hasPlayerOrigin);
        return false;
    }

    // Records a naval setup failure on the guard, which ends the mission; the exception still reaches the tick's log.
    [HarmonyPatch(typeof(DeploymentMissionController), nameof(DeploymentMissionController.SetupTeams))]
    [HarmonyFinalizer]
    private static Exception SetupTeamsFinalizer(DeploymentMissionController __instance, Exception __exception)
    {
        if (__exception == null || !CoopNavalMissionScope.IsActive || __instance is not NavalDeploymentMissionController)
            return __exception;

        if (CoopNavalSetupGuard.TryEnd(__instance.Mission, NavalSetupEndReason.TeamSetupFailed,
                "team setup threw, vanilla retries it next tick"))
            Logger.Error(__exception, "[NavalBattle] Naval team setup failed; ending the mission instead of retrying it every tick");
        return __exception;
    }

    // Skips the deployment controller's tick once its naval team setup failed, so the side deployment never runs twice.
    [HarmonyPatch(typeof(DeploymentMissionController), nameof(DeploymentMissionController.OnMissionTick))]
    [HarmonyPrefix]
    private static bool OnMissionTickPrefix(DeploymentMissionController __instance)
    {
        if (!CoopNavalMissionScope.IsActive || __instance is not NavalDeploymentMissionController) return true;

        return __instance.Mission?.GetMissionBehavior<CoopNavalSetupGuard>()?.SetupFailed != true;
    }
}
