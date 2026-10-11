using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalTransitionCampaignBehavior))]
internal class NavalTransitionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalTransitionCampaignBehavior.wait_here_on_condition))]
    [HarmonyPostfix]
    public static void WaitHereOnConditionPostfix(ref bool __result)
    {
        __result = false;
    }
}
