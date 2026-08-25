using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace E2E.Tests.Services.Issues;

internal static class TestQuestTypeFixture
{
    private static readonly object Lock = new();

    internal static void EnsureVillageNeedsToolsRegistered()
    {
        lock (Lock)
        {
            if (QuestTypeRegistry.IsRegistered(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue))) return;

            var descriptor = QuestDescriptorBuilder
                .For<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>("VillageNeedsTools")
                .WithQuestSolutionAccept()
                .WithAlternativeAccept()
                .Build();

            QuestTypeRegistry.Register(descriptor);
        }
    }
}
