using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.RaidAnEnemyTerritory;

using Issue = RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;
using Quest = RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

[QuestTypeModule]
internal static class RaidAnEnemyTerritoryQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._raidedVillages.Count >= 3;
    }

    static RaidAnEnemyTerritoryQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("RaidAnEnemyTerritory")
            .WithQuestSolutionAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
