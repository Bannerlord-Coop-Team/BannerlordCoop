using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class HeadmanNeedsGrainCompanionSelectionPatches
{
    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static bool SelectCompanionPrefix()
    {
        if (ModInformation.IsServer || !(IssuesCampaignBehavior.GetIssueOwnersIssue() is Issue issue)) return true;
        if (ConversationSentence.SelectedRepeatObject is Hero hero)
            issue.AlternativeSolutionSentTroops.AddToCounts(hero.CharacterObject, 1);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static bool CancelSelectionPrefix()
    {
        if (ModInformation.IsServer || !(IssuesCampaignBehavior.GetIssueOwnersIssue() is Issue issue)) return true;
        issue.AlternativeSolutionSentTroops.Clear();
        return false;
    }
}
