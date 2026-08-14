namespace GameInterface.Services.Issues.Generic.Migrated.ArmyNeedsSupplies;

using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

[QuestTypeModule]
internal static class ArmyNeedsSuppliesQuestType
{
    static ArmyNeedsSuppliesQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("ArmyNeedsSupplies")
            .WithQuestSolutionAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
