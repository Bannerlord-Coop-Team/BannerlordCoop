using Common.Logging;
using HarmonyLib;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// With no own troops vanilla assigns the player's team no ship, then puts the player troop on that missing hull every tick
/// (NRE in NavalAgentsLogic.AddReservedTroopToShip, R41a); the allocation is skipped and the mission ends instead.
/// </summary>
[HarmonyPatch(typeof(NavalTeamSideSpawnContext), nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalShiplessPlayerTeamPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalShiplessPlayerTeamPatch>();

    // Runs the player team's initial allocation only when its first formation has a ship; otherwise the reserve guard ends
    // the mission, since a player with no hull has nothing to do in it.
    [HarmonyPrefix]
    private static bool Prefix(NavalTeamSideSpawnContext __instance)
    {
        if (AllocatesPlayerTeam(__instance._shipsLogic.GetShipAssignment(TeamSideEnum.PlayerTeam, FormationClass.Infantry)))
            return true;

        var guard = Mission.Current?.GetMissionBehavior<CoopNavalReserveGuard>();
        if (guard == null)
        {
            Logger.Error("[NavalBattle] The player's team has no assigned ship and no reserve guard ends the mission; skipping its initial troop allocation");
            return false;
        }

        if (guard.RequestEnd())
            Logger.Warning("[NavalBattle] The player's team has no assigned ship; skipping its initial troop allocation and ending the mission");
        return false;
    }

    // Whether vanilla's player-team allocation has a ship to put the player troop on.
    internal static bool AllocatesPlayerTeam(ShipAssignment assignment) => assignment?.HasMissionShip == true;
}
