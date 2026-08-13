using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.EscortMerchantCaravan;

[QuestTypeModule]
internal static class EscortMerchantCaravanQuestType
{
    static EscortMerchantCaravanQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("EscortMerchantCaravan")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(EscortMerchantCaravanIssueBehavior));
    }
}
