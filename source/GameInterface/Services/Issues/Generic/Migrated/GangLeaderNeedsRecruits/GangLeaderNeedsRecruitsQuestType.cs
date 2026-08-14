using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsRecruits;

[QuestTypeModule]
internal static class GangLeaderNeedsRecruitsQuestType
{
    static GangLeaderNeedsRecruitsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("GangLeaderNeedsRecruits")
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
