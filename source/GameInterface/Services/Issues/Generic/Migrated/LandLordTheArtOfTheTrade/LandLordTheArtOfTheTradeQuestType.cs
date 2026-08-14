using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.LandLordTheArtOfTheTrade;

using Issue = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

[QuestTypeModule]
internal static class LandLordTheArtOfTheTradeQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return quest._gatheredDenars >= quest._targetDenarsToAchieve;
    }

    static LandLordTheArtOfTheTradeQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LandLordTheArtOfTheTrade")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior));
    }
}
