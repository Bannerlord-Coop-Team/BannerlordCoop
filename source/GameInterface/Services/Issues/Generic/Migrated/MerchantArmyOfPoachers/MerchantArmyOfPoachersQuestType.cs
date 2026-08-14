using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated.MerchantArmyOfPoachers;

using Issue = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;
using Quest = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

[QuestTypeModule]
internal static class MerchantArmyOfPoachersQuestType
{
    static MerchantArmyOfPoachersQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("MerchantArmyOfPoachers")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
