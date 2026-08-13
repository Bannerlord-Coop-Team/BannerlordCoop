using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[QuestTypeModule]
internal static class GangLeaderNeedsWeaponsQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        var collected = 0;
        foreach (var element in party.ItemRoster)
        {
            var item = element.EquipmentElement.Item;
            if (item != null && item.WeaponComponent != null && item.WeaponComponent.PrimaryWeapon.WeaponClass == quest._requestedWeaponClass)
            {
                collected += element.Amount;
            }
        }

        return collected >= quest._requestedWeaponAmount;
    }

    static GangLeaderNeedsWeaponsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("GangLeaderNeedsWeapons")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior));
    }
}
