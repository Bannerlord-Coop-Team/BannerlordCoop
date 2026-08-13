using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.CaravanAmbush;

using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

[QuestTypeModule]
internal static class CaravanAmbushQuestType
{
    static CaravanAmbushQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("CaravanAmbush")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior));
    }
}
