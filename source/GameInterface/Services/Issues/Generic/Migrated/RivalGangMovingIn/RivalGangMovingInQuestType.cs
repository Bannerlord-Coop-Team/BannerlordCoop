using TaleWorlds.CampaignSystem.Party;
using Issue = SandBox.Issues.RivalGangMovingInIssueBehavior.RivalGangMovingInIssue;
using Quest = SandBox.Issues.RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.RivalGangMovingIn;

[QuestTypeModule]
internal static class RivalGangMovingInQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._rivalGangLeader?.IsDead == true;
    }

    static RivalGangMovingInQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("RivalGangMovingIn")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(SandBox.Issues.RivalGangMovingInIssueBehavior));
    }
}
