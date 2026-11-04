using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// Vanilla's cancel returns sent troops to <c>MobileParty.MainParty</c>, which the dedicated server does not have,
/// so issues are finalized directly and only the companions among the troops are freed.
/// </summary>
internal class IssueDismisser : IIssueDismisser
{
    public void Dismiss(IssueBase issue)
    {
        if (issue.IsSolvingWithAlternative)
        {
            var settlement = issue.IssueOwner?.CurrentSettlement ?? issue.IssueOwner?.HomeSettlement;

            foreach (var element in issue.AlternativeSolutionSentTroops.GetTroopRoster())
            {
                if (element.Character.IsHero && element.Character.HeroObject.HeroState == Hero.CharacterStates.Disabled)
                {
                    FreeCompanion(element.Character.HeroObject, settlement);
                }
            }
        }

        issue.IssueFinalized();
    }

    private static void FreeCompanion(Hero companion, Settlement settlement)
    {
        if (settlement == null)
        {
            companion.ChangeState(Hero.CharacterStates.Active);
            return;
        }

        TeleportHeroAction.ApplyImmediateTeleportToSettlement(companion, settlement);
    }
}
