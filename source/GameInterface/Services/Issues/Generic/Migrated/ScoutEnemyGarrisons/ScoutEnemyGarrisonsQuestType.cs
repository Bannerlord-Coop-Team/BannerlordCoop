using TaleWorlds.CampaignSystem.Party;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Generic.Migrated.ScoutEnemyGarrisons;

[QuestTypeModule]
internal static class ScoutEnemyGarrisonsQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._scoutedSettlementCount >= 3;
    }

    static ScoutEnemyGarrisonsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("ScoutEnemyGarrisons")
            .WithQuestSolutionAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
