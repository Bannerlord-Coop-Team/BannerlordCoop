using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;

using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

[QuestTypeModule]
internal static class VillageNeedsToolsQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._requestedTradeGood) >= quest._numberOfRequestedGood;
    }

    static VillageNeedsToolsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("VillageNeedsTools")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
