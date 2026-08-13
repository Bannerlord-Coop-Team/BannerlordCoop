using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.NearbyBanditBase;

using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

[QuestTypeModule]
internal static class NearbyBanditBaseQuestType
{
    static NearbyBanditBaseQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("NearbyBanditBase")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior));
    }
}
