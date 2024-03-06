using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditsCampaignBehavior))]
internal class DisableBanditsCampaignBehavior
{
    [HarmonyPatch(nameof(BanditsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPatch(nameof(BanditsCampaignBehavior.WeeklyTick))]
    [HarmonyPatch(nameof(BanditsCampaignBehavior.DailyTick))]
    [HarmonyPatch(nameof(BanditsCampaignBehavior.HourlyTick))]
    [HarmonyPatch("OnNewGameCreatedPartialFollowUp")]
    static bool Prefix() => false;
}
