using Common.Logging;
using HarmonyLib;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Naval team setups vanilla would retry every tick end the mission through <see cref="NavalSetupGuard"/> instead: a
/// player team it can't allocate (R41a, R41a-2 S) and a setup that threw, which would spawn a hull per formation each time.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalTeamSetupFailurePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalTeamSetupFailurePatches>();

    // Runs the player team's initial allocation only with a ship and a player origin to put on it; it would throw every tick
    // otherwise. The mission ends, as in vanilla where a wounded hero can't enter a battle. Coop battles only: a raid keeps
    // vanilla's allocation, and a throw there still ends through the finalizer below.
    [HarmonyPatch(typeof(NavalTeamSideSpawnContext), nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam))]
    [HarmonyPrefix]
    private static bool AllocationPrefix(NavalTeamSideSpawnContext __instance)
    {
        if (!NavalMissionTracker.IsActiveHere()) return true;

        bool hasShip = __instance._shipsLogic.GetShipAssignment(TeamSideEnum.PlayerTeam, FormationClass.Infantry)?.HasMissionShip == true;
        bool hasPlayerOrigin = __instance._agentsLogic.FindTroopOrigin(__instance.TeamSide, origin => origin.Troop.IsPlayerCharacter) != null;
        if (hasShip && hasPlayerOrigin) return true;

        if (Mission.Current?.GetMissionBehavior<NavalSetupGuard>()?.End(NavalSetupEndReason.PlayerTeamNotAllocatable) == true)
            Logger.Warning("[NavalBattle] The player's team can't be allocated (ship={HasShip} hero={HasHero}); skipping its initial troop allocation and ending the mission",
                hasShip, hasPlayerOrigin);
        return false;
    }

    // Records a naval setup failure on the guard, which ends the mission; the exception still reaches the tick's log. Keyed
    // on the guard because a raid's controller derives straight from DeploymentMissionController.
    [HarmonyPatch(typeof(DeploymentMissionController), nameof(DeploymentMissionController.SetupTeams))]
    [HarmonyFinalizer]
    private static Exception SetupTeamsFinalizer(DeploymentMissionController __instance, Exception __exception)
    {
        if (__exception == null) return __exception;

        if (__instance.Mission?.GetMissionBehavior<NavalSetupGuard>()?.End(NavalSetupEndReason.TeamSetupFailed) == true)
            Logger.Error(__exception, "[NavalBattle] Naval team setup failed; ending the mission instead of retrying it every tick");
        return __exception;
    }

    // Skips the deployment controller's tick once its naval team setup failed, so the side deployment never runs twice.
    [HarmonyPatch(typeof(DeploymentMissionController), nameof(DeploymentMissionController.OnMissionTick))]
    [HarmonyPrefix]
    private static bool OnMissionTickPrefix(DeploymentMissionController __instance) =>
        __instance.Mission?.GetMissionBehavior<NavalSetupGuard>()?.SetupFailed != true;
}
