using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.LordNeedsGarrisonTroops;

using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

[QuestTypeModule]
internal static class LordNeedsGarrisonTroopsQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.MemberRoster.GetTroopCount(quest._requestedTroopType) >= quest._requestedTroopAmount;
    }

    static LordNeedsGarrisonTroopsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LordNeedsGarrisonTroops")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior));
    }
}
