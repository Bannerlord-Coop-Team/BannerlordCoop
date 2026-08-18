using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class IssuesCampaignBehaviorGenerationPatches
{
    [HarmonyPatch("DailyTickClan")]
    [HarmonyPrefix]
    private static bool DailyTickClanPrefix() => ModInformation.IsServer;

    [HarmonyPatch("OnSettlementDailyTick")]
    [HarmonyPrefix]
    private static bool OnSettlementDailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch("OnNewGameCreatedPartialFollowUpEnd")]
    [HarmonyPrefix]
    private static bool OnNewGameCreatedPartialFollowUpEndPrefix() => ModInformation.IsServer;

    [HarmonyPatch("OnSettlementEntered")]
    [HarmonyPrefix]
    private static bool OnSettlementEnteredPrefix() => ModInformation.IsServer;
}
