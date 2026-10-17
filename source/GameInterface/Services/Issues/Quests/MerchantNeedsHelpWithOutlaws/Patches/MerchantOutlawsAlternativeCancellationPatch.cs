using Common.Messaging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
internal class MerchantOutlawsAlternativeCancellationPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (!(Hero.OneToOneConversationHero?.Issue is Issue issue)) return true;
        MessageBroker.Instance.Publish(issue, new MerchantOutlawsAlternativeCancelled(issue));
        return false;
    }
}
