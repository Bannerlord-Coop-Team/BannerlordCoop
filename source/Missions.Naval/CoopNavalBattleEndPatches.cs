using HarmonyLib;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// The spawn logic counts reserve on every hull, so troops queued on a sunk or handed-over hull would keep a side alive.
[HarmonyPatch(typeof(DefaultNavalMissionAgentSpawnLogic), nameof(DefaultNavalMissionAgentSpawnLogic.IsSideDepleted))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalSideDepletionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(DefaultNavalMissionAgentSpawnLogic __instance, BattleSideEnum side, ref bool __result)
    {
        var endLogic = __instance.Mission?.GetMissionBehavior<CoopNavalBattleEndLogic>();
        if (endLogic == null) return true;

        __result = endLogic.IsSideDepletedHere(side);
        return false;
    }
}

// OnMissionEnd lists the losing side's ships for campaign capture, and coop mission ships are detached snapshots.
[HarmonyPatch(typeof(NavalBattleEndLogic), nameof(NavalBattleEndLogic.OnMissionEnd))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalShipCapturePatch
{
    [HarmonyPrefix]
    private static bool Prefix(NavalBattleEndLogic __instance) => __instance is not CoopNavalBattleEndLogic;
}

// Vanilla counts a hull's crew from its NavalShipAgents, which a copy never fills, so every foreign hull read as crewless.
[HarmonyPatch(typeof(NavalBattleEndLogic), nameof(NavalBattleEndLogic.AreAnySideShipsOutOfAction))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CoopNavalHullsOutOfActionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NavalBattleEndLogic __instance, BattleSideEnum playerSide, BattleSideEnum enemySide,
        ref bool playerShipsOutOfAction, ref bool enemyShipsOutOfAction, ref bool __result)
    {
        if (__instance is not CoopNavalBattleEndLogic endLogic) return true;

        __result = endLogic.AreAnySideHullsOutOfAction(playerSide, enemySide, out playerShipsOutOfAction, out enemyShipsOutOfAction);
        return false;
    }
}
