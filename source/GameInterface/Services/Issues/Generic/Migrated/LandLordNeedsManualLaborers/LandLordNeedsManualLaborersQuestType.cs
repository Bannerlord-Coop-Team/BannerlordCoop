using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.LandLordNeedsManualLaborers;

using Issue = TaleWorlds.CampaignSystem.Issues.LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest;

[QuestTypeModule]
internal static class LandLordNeedsManualLaborersQuestType
{
    static LandLordNeedsManualLaborersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LandLordNeedsManualLaborers")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LandLordNeedsManualLaborersIssueBehavior));
    }
}
