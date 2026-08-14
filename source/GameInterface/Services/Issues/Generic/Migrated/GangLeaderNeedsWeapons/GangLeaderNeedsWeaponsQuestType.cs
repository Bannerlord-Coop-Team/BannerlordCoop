namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[QuestTypeModule]
internal static class GangLeaderNeedsWeaponsQuestType
{
    static GangLeaderNeedsWeaponsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("GangLeaderNeedsWeapons")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
