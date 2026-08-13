using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.ScoutEnemyGarrisons;

[QuestTypeModule]
internal static class ScoutEnemyGarrisonsQuestType
{
    static ScoutEnemyGarrisonsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("ScoutEnemyGarrisons")
            .WithQuestSolutionAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior));
    }
}
