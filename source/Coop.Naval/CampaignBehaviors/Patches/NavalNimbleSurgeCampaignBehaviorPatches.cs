using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalNimbleSurgeCampaignBehaviour))]
internal class NavalNimbleSurgeCampaignBehaviourPatches
{
    [HarmonyPatch(nameof(NavalNimbleSurgeCampaignBehaviour.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalNimbleSurgeCampaignBehaviour.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;
}
