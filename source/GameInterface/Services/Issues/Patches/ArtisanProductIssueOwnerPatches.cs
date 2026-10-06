using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.ChangeIssueOwner))]
internal sealed class ArtisanProductIssueOwnerPatches
{
    [HarmonyPrefix]
    internal static bool BeforeChange(IssueBase issue, out Hero __state)
    {
        __state = issue.IssueOwner;
        return issue is not Issue || ModInformation.IsServer || AllowedThread.IsThisThreadAllowed();
    }

    [HarmonyPostfix]
    private static void AfterChange(IssueBase issue, Hero __state, bool __runOriginal)
    {
        if (ModInformation.IsServer && __runOriginal && issue is Issue artisan && issue.IssueOwner != __state)
            MessageBroker.Instance.Publish(issue, new ArtisanProductIssueOwnerChanged(artisan, __state));
    }
}
