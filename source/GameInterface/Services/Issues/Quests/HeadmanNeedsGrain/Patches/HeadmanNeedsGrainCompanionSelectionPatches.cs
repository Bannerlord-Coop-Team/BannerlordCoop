using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class HeadmanNeedsGrainCompanionSelectionPatches
{
    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyPrefix]
    private static bool SelectCompanionPrefix()
    {
        if (ModInformation.IsServer || !(IssuesCampaignBehavior.GetIssueOwnersIssue() is Issue issue)) return true;
        // The server removes the selected troops after accepting the request.
        if (ConversationSentence.SelectedRepeatObject is Hero hero)
            issue.AlternativeSolutionSentTroops.AddToCounts(hero.CharacterObject, 1);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    [HarmonyPrefix]
    private static bool CancelSelectionPrefix()
    {
        if (ModInformation.IsServer || !(IssuesCampaignBehavior.GetIssueOwnersIssue() is Issue issue)) return true;
        issue.AlternativeSolutionSentTroops.Clear();
        return false;
    }
}
