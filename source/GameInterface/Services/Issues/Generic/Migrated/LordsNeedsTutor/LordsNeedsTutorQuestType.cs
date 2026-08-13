using TaleWorlds.CampaignSystem.Party;

using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.LordsNeedsTutor;

[QuestTypeModule]
internal static class LordsNeedsTutorQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._youngHero.HeroDeveloper.GetTotalSkillPoints() >= quest._targetSkillGain;
    }

    static LordsNeedsTutorQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LordsNeedsTutor")
            .WithQuestSolutionAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior));
    }
}
