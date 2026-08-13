using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Patches;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated.RaidAnEnemyTerritory;

using Issue = RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;
using Quest = RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

[QuestTypeModule]
internal static class RaidAnEnemyTerritoryQuestType
{
    static RaidAnEnemyTerritoryQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("RaidAnEnemyTerritory")
            .WithQuestSolutionAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(RaidAnEnemyTerritoryIssueBehavior));
    }
}
