using HarmonyLib;
using SandBox.Missions;
using SandBox.Missions.MissionLogics.Hideout;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

[HarmonyPatch]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutDepletionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HideoutMissionController), nameof(HideoutMissionController.IsSideDepleted));
        yield return AccessTools.Method(typeof(HideoutAmbushMissionController), nameof(HideoutAmbushMissionController.IsSideDepleted));
    }

    private static bool Prefix(BattleSideEnum side, ref bool __result)
    {
        var coop = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (coop == null) return true;
        __result = coop.IsSideDepleted(side);
        return false;
    }
}

[HarmonyPatch(typeof(HideoutAmbushMissionController), nameof(HideoutAmbushMissionController.OnStealthMissionCounterFailed))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutStealthFailurePatch
{
    private static bool Prefix()
    {
        var coop = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (coop == null) return true;
        coop.OnStealthFailure();
        return false;
    }
}

[HarmonyPatch(typeof(StealthFailCounterMissionLogic), nameof(StealthFailCounterMissionLogic.ShowMissionFailedPopup))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutStealthFailurePopupPatch
{
    private static bool Prefix()
    {
        var coop = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (coop == null) return true;
        coop.OnStealthFailure();
        return false;
    }
}

[HarmonyPatch(typeof(HideoutAmbushMissionController), nameof(HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight))]
[HarmonyPatchCategory(MissionModule.HideoutPatchCategory)]
internal static class HideoutBossReservePatch
{
    internal static void Prefix(HideoutAmbushMissionController __instance, ref int spawnCount)
    {
        // Native pads with unregistered PartyGroupAgentOrigin clones; the raid only fields its real reserve.
        if (__instance is CoopHideoutAmbushController)
            spawnCount = System.Math.Min(spawnCount, __instance._allEnemyTroops.Count);
    }
}
