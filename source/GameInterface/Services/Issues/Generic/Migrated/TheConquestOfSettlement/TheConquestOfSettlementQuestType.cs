using TaleWorlds.CampaignSystem.Party;
using Issue = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

namespace GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;

[QuestTypeModule]
internal static class TheConquestOfSettlementQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._targetSettlement.MapFaction == issue.IssueOwner.MapFaction;
    }

    static TheConquestOfSettlementQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("TheConquestOfSettlement")
            .WithQuestSolutionAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
