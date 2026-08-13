using System.Linq;
using TaleWorlds.CampaignSystem.Party;

using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.LordWantsRivalCaptured;

[QuestTypeModule]
internal static class LordWantsRivalCapturedQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.PrisonRoster.GetTroopRoster().Any(x => x.Character.IsHero && x.Character.HeroObject == quest._targetHero);
    }

    static LordWantsRivalCapturedQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("LordWantsRivalCaptured")
            .WithQuestSolutionAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior));
    }
}
