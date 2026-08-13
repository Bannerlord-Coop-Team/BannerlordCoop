using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.HeadmanNeedsGrain;

using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

[QuestTypeModule]
internal static class HeadmanNeedsGrainQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(DefaultItems.Grain) >= quest._neededGrainAmount;
    }

    static HeadmanNeedsGrainQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("HeadmanNeedsGrain")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior));
    }
}
