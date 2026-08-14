using TaleWorlds.CampaignSystem.Party;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.Smugglers;

using Issue = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

[QuestTypeModule]
internal static class SmugglersQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._smugglerParty == null || !quest._smugglerParty.IsActive;
    }

    static SmugglersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("Smugglers")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
