using TaleWorlds.CampaignSystem.Party;

using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.HeadmanVillageNeedsDraughtAnimals;

[QuestTypeModule]
internal static class HeadmanVillageNeedsDraughtAnimalsQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._requestedAnimal) >= quest._requestedAnimalAmount;
    }

    static HeadmanVillageNeedsDraughtAnimalsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("HeadmanVillageNeedsDraughtAnimals")
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.HeadmanVillageNeedsDraughtAnimalsIssueBehavior));
    }
}
