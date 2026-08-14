using TaleWorlds.CampaignSystem.Party;

using Issue = TaleWorlds.CampaignSystem.Issues.LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.LandlordTrainingForRetainers;

[QuestTypeModule]
internal static class LandlordTrainingForRetainersQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.MemberRoster.GetTroopCount(quest._questTargetChar) > 0;
    }

    static LandlordTrainingForRetainersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LandlordTrainingForRetainers")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LandlordTrainingForRetainersIssueBehavior));
    }
}
