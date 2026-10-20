using Common.Logging;
using HarmonyLib;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// Vanilla's initial player allocation puts the player troop on the player team's first ship; with no ship (no troops, R41a)
/// or no player hero in the reserve (R41a-2 S) it throws every tick, so the allocation is skipped and the mission ends.
/// </summary>
[HarmonyPatch(typeof(NavalTeamSideSpawnContext), nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalPlayerTeamAllocationPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalPlayerTeamAllocationPatch>();

    // Runs the allocation only with a ship and a player origin to put on it; otherwise the reserve guard ends the mission,
    // matching vanilla where a wounded hero can't enter a battle.
    [HarmonyPrefix]
    private static bool Prefix(NavalTeamSideSpawnContext __instance)
    {
        bool hasShip = __instance._shipsLogic.GetShipAssignment(TeamSideEnum.PlayerTeam, FormationClass.Infantry)?.HasMissionShip == true;
        bool hasPlayerOrigin = __instance._agentsLogic.FindTroopOrigin(__instance.TeamSide, origin => origin.Troop.IsPlayerCharacter) != null;
        if (AllocatesPlayerTeam(hasShip, hasPlayerOrigin)) return true;

        var guard = Mission.Current?.GetMissionBehavior<CoopNavalReserveGuard>();
        if (guard == null)
        {
            Logger.Error("[NavalBattle] The player's team can't be allocated (ship={HasShip} hero={HasHero}) and no reserve guard ends the mission; skipping its initial troop allocation",
                hasShip, hasPlayerOrigin);
            return false;
        }

        if (guard.RequestEnd())
            Logger.Warning("[NavalBattle] The player's team can't be allocated (ship={HasShip} hero={HasHero}); skipping its initial troop allocation and ending the mission",
                hasShip, hasPlayerOrigin);
        return false;
    }

    // Whether vanilla's player-team allocation has both a ship and the player troop to put on it.
    internal static bool AllocatesPlayerTeam(bool hasShip, bool hasPlayerOrigin) => hasShip && hasPlayerOrigin;
}
