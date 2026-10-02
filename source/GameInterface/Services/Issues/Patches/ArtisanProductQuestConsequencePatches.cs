using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[HarmonyPatch(typeof(Quest))]
internal sealed class ArtisanProductQuestConsequencePatches
{
    [HarmonyPatch("QuestAcceptedConsequences")]
    [HarmonyPrefix]
    private static bool Accepted() => ModInformation.IsServer;

    [HarmonyPatch("OnCompleteWithSuccess")]
    [HarmonyPrefix]
    private static bool CompleteWithSuccess() => ModInformation.IsServer;

    [HarmonyPatch("OnFailed")]
    [HarmonyPrefix]
    private static bool Failed() => ModInformation.IsServer;

    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static bool TimedOut() => ModInformation.IsServer;
}
