using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System.Linq;
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
    private static readonly ILogger Logger = LogManager.GetLogger<IssueDismisser>();

    public void Dismiss(IssueBase issue)
    {
        if (issue.IsSolvingWithAlternative)
        {
            LeaveBehindDeadCompanions(issue);
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

    public void LeaveBehindDeadCompanions(IssueBase issue)
    {
        var troops = issue.AlternativeSolutionSentTroops;
        for (var index = troops.Count - 1; index >= 0; index--)
        {
            var hero = troops.GetCharacterAtIndex(index).HeroObject;
            // Vanilla drops Dead heroes, and one whose death cleared it without making it Dead throws in OnHeroActivated
            if (hero == null || (!hero.IsDead && hero.HeroDeveloper != null)) continue;

            Logger.Warning("Leaving {hero} sent for {issue} behind, its death already ran", hero.StringId, issue.StringId);
            var element = troops.GetElementCopyAtIndex(index);
            troops.AddToCounts(hero.CharacterObject, -element.Number, woundedCount: -element.WoundedNumber);
        }
    }

    // DeactivateIssue clears the hero's pointer before it removes the issue, so only the manager says it is out
    public bool IsInManager(IssueBase issue) =>
        Campaign.Current.IssueManager.Issues.Values.Any(registered => ReferenceEquals(registered, issue));

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
