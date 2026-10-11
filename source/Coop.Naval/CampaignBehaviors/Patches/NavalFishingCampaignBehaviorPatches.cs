using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalFishingCampaignBehaviour))]
internal class NavalFishingCampaignBehaviourPatches
{
    [HarmonyPatch(nameof(NavalFishingCampaignBehaviour.OnDailyTickSettlement))]
    [HarmonyPrefix]
    public static bool OnDailySettlementTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalFishingCampaignBehaviour.OnHourlyTickParty))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPartyPrefix() => ModInformation.IsServer;
}
