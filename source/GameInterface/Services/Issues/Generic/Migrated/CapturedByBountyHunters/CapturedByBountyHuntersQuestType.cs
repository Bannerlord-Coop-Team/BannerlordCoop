using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;

using Issue = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

[QuestTypeModule]
internal static class CapturedByBountyHuntersQuestType
{
    static CapturedByBountyHuntersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("CapturedByBountyHunters")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior));
    }
}
