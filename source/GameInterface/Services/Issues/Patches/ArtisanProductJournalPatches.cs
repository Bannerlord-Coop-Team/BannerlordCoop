using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch]
internal sealed class ArtisanProductJournalPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void BeforeFinalization(IssueBase __instance)
    {
        if (ModInformation.IsServer && IssueFinalizeAuthorityGuard.IsActive &&
            __instance is ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue &&
            issue.IssueOwner?.Issue == issue)
            // Vanilla reports decree success after IssueFinalized, too late for the removal snapshot.
            MessageBroker.Instance.Publish(issue, new ArtisanProductJournalChanged(issue,
                ArtisanProductLordActionPatches.CompletingDecree == issue ? IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess : null));
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPostfix]
    private static void AfterDailyProgress(IssueManager __instance)
    {
        if (ModInformation.IsClient) return;
        foreach (var issue in __instance.Issues.Values)
            if (issue is ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue artisan &&
                artisan.IsSolvingWithAlternative)
                MessageBroker.Instance.Publish(issue, new ArtisanProductJournalChanged(artisan));
    }
}
