using Issue = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;

[QuestTypeModule]
internal static class TheConquestOfSettlementQuestType
{
    static TheConquestOfSettlementQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("TheConquestOfSettlement")
            .WithQuestSolutionAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior));
    }
}
