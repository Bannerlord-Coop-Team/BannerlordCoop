using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ArtisanProductsCounterOfferConversationPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        foreach (var name in new[]
        {
            nameof(IssuesCampaignBehavior.issue_counter_offer_2_condition),
            nameof(IssuesCampaignBehavior.issue_counter_offer_player_accept_condition),
            nameof(IssuesCampaignBehavior.issue_counter_offer_player_reject_condition),
            nameof(IssuesCampaignBehavior.issue_counter_offer_accepted_condition),
            nameof(IssuesCampaignBehavior.issue_counter_offer_reject_condition),
            nameof(IssuesCampaignBehavior.issue_counter_offer_accepted_consequence),
            nameof(IssuesCampaignBehavior.issue_counter_offer_reject_consequence),
        })
        {
            yield return AccessTools.Method(typeof(IssuesCampaignBehavior), name);
        }
    }

    [HarmonyPrefix]
    private static bool Prefix()
        => ModInformation.IsServer || IssuesCampaignBehavior.GetCounterOfferersIssue() != null;
}
