using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalInitializationCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalInitializationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalInitializationCampaignBehavior.OnAfterSessionLaunched))]
    [HarmonyPrefix]
    public static bool OnAfterSessionLaunchedPrefix() => ModInformation.IsServer;
}
