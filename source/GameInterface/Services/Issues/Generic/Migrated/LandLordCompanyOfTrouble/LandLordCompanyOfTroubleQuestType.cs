using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.LandLordCompanyOfTrouble;

[QuestTypeModule]
internal static class LandLordCompanyOfTroubleQuestType
{
    static LandLordCompanyOfTroubleQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LandLordCompanyOfTrouble")
            .WithQuestSolutionAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior));
    }
}
