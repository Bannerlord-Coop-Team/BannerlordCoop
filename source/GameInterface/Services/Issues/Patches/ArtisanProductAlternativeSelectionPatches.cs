using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Dispatch;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal sealed class ArtisanProductAlternativeSelectionPatches
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void RestoreSelectionAfterClaim(IssueBase __instance)
    {
        if (ModInformation.IsServer || __instance is not Issue issue) return;
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;
        if (QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept != true || !issue.IsOngoingWithoutQuest) return;

        // The generic trigger has packed the claim; server roster deltas commit the accepted selection.
        using (new AllowedThread())
        {
            MobileParty.MainParty.MemberRoster.Add(issue.AlternativeSolutionSentTroops);
            issue.AlternativeSolutionSentTroops.Clear();
        }
    }
}
