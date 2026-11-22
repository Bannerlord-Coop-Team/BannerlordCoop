using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

/// <summary>
/// Hides the coastal village ferry option on clients, because ferry travel isn't synced yet (#4604).
/// </summary>
[HarmonyPatch(typeof(FerryCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class FerryCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(FerryCampaignBehavior.game_menu_ferry_target_on_condition))]
    [HarmonyPostfix]
    public static void GameMenuFerryTargetOnConditionPostfix(ref bool __result)
    {
        if (ModInformation.IsClient) __result = false;
    }
}
