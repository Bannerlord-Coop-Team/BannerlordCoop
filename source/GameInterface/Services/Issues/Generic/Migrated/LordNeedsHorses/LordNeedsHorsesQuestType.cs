using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.LordNeedsHorses;

using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

[QuestTypeModule]
internal static class LordNeedsHorsesQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._mountObjectToBeDelivered) >= quest._numMountsToBeDelivered;
    }

    static LordNeedsHorsesQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LordNeedsHorses")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
