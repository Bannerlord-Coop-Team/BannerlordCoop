using TaleWorlds.CampaignSystem.Party;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Generic.Migrated.LandLordCompanyOfTrouble;

[QuestTypeModule]
internal static class LandLordCompanyOfTroubleQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return party != null && party.MemberRoster.GetTroopCount(quest._troubleCharacterObject) == 0;
    }

    static LandLordCompanyOfTroubleQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LandLordCompanyOfTrouble")
            .WithQuestSolutionAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
