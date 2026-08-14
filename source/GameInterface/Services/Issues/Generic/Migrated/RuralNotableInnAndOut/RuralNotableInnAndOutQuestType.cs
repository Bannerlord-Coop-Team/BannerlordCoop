using TaleWorlds.CampaignSystem.Party;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.RuralNotableInnAndOut;

using Issue = SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue;
using Quest = SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssueQuest;

[QuestTypeModule]
internal static class RuralNotableInnAndOutQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._playerWonTheGame || quest._applyLesserReward;
    }

    static RuralNotableInnAndOutQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("RuralNotableInnAndOut")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
