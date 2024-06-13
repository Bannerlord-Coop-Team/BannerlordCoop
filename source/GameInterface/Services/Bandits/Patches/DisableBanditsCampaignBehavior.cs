using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditsCampaignBehavior))]
internal class DisableBanditsCampaignBehavior
{
    [HarmonyPatch(nameof(BanditsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    static bool OnSettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BanditsCampaignBehavior.WeeklyTick))]
    [HarmonyPrefix]
    static bool WeeklyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BanditsCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    static bool DailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BanditsCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    static bool HourlyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch("OnNewGameCreatedPartialFollowUp")]
    static bool Prefix() => false;

    [HarmonyPatch(nameof(BanditsCampaignBehavior.OnNewGameCreated))]
    [HarmonyPrefix]
    static bool OnNewGameCreatedPrefix() => ModInformation.IsServer;
}
