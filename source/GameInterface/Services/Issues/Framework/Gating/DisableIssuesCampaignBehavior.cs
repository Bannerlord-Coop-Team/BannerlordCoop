using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Framework.Gating;

// These handlers create or end issues, should only be ran on server
[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class DisableIssuesCampaignBehavior
{
    [HarmonyPatch(nameof(IssuesCampaignBehavior.OnSettlementDailyTick))]
    [HarmonyPrefix]
    private static bool OnSettlementDailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(IssuesCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    private static bool DailyTickClanPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(IssuesCampaignBehavior.OnGameLoaded))]
    [HarmonyPrefix]
    private static bool OnGameLoadedPrefix() => ModInformation.IsServer;
}
