using TaleWorlds.CampaignSystem.Party;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.NearbyBanditBase;

using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

[QuestTypeModule]
internal static class NearbyBanditBaseQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._targetHideout?.Parties.Count == 0;
    }

    static NearbyBanditBaseQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("NearbyBanditBase")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
