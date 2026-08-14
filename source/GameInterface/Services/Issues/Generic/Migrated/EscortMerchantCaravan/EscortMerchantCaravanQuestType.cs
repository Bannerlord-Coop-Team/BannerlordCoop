using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Generic.Migrated.EscortMerchantCaravan;

[QuestTypeModule]
internal static class EscortMerchantCaravanQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._visitedSettlements.Count > 0;
    }

    static EscortMerchantCaravanQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("EscortMerchantCaravan")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
