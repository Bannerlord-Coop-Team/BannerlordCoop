using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalInitializationCampaignBehavior))]
internal class NavalInitializationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalInitializationCampaignBehavior.OnAfterSessionLaunched))]
    [HarmonyPrefix]
    public static bool OnAfterSessionLaunchedPrefix() => ModInformation.IsServer;
}
