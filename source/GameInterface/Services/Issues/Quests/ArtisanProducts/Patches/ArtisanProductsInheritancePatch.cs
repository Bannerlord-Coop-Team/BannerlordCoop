using Common;
using Common.Messaging;
using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.ChangeIssueOwner))]
internal class ArtisanProductsInheritancePatch
{
    [HarmonyPrefix]
    private static bool Before(IssueBase issue, out Hero __state)
    {
        __state = issue.IssueOwner;
        return !(issue is Issue) || ModInformation.IsServer || AllowedThread.IsThisThreadAllowed();
    }

    [HarmonyPostfix]
    private static void After(IssueBase issue, Hero __state, bool __runOriginal)
    {
        if (ModInformation.IsServer && __runOriginal && issue is Issue artisan && issue.IssueOwner != __state)
            MessageBroker.Instance.Publish(issue, new ArtisanProductsOwnerChanged(artisan, __state));
    }
}
