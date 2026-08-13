using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.LandLordTheArtOfTheTrade;

using Issue = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

[QuestTypeModule]
internal static class LandLordTheArtOfTheTradeQuestType
{
    static LandLordTheArtOfTheTradeQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LandLordTheArtOfTheTrade")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior));
    }
}
