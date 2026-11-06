using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(FishingPartyCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class FishingPartyCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(FishingPartyCampaignBehavior.OnDailySettlementTick))]
    [HarmonyPrefix]
    public static bool OnDailySettlementTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(FishingPartyCampaignBehavior.OnHourlyTickParty))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(FishingPartyCampaignBehavior.OnGameEarlyLoaded))]
    [HarmonyPrefix]
    public static bool OnGameEarlyLoadedPrefix() => ModInformation.IsServer;
}
