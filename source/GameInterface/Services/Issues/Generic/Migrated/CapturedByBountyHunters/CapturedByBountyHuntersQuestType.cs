using TaleWorlds.CampaignSystem.Party;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;

using Issue = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

[QuestTypeModule]
internal static class CapturedByBountyHuntersQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._questHideout?.Parties.Count == 0;
    }

    static CapturedByBountyHuntersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("CapturedByBountyHunters")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
