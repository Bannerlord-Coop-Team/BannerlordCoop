using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// Vanilla's cancel returns sent troops to <c>MobileParty.MainParty</c>, which the dedicated server does not have,
/// so issues are finalized directly and only the companions among the troops are freed.
/// </summary>
internal class IssueDismisser : IIssueDismisser
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueDismisser>();
    private const string ReturnedTroopsText = "{=V5Za6d4h}Your troops have returned from their mission.";

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
            if (hero == null) continue;
            // Vanilla drops Dead heroes, and one whose death cleared it without making it Dead throws in OnHeroActivated
            var deathAlreadyRan = hero.IsDead || hero.HeroDeveloper == null;
            if (!deathAlreadyRan) continue;

            Logger.Warning("Leaving {hero} sent for {issue} behind, its death already ran", hero.StringId, issue.StringId);
            var element = troops.GetElementCopyAtIndex(index);
            troops.AddToCounts(hero.CharacterObject, -element.Number, woundedCount: -element.WoundedNumber);
        }
    }

    // DeactivateIssue clears the hero's pointer before it removes the issue, so only the manager says it is out
    public bool IsInManager(IssueBase issue) =>
        Campaign.Current.IssueManager.Issues.Values.Any(registered => ReferenceEquals(registered, issue));

    public void ForceRemove(IssueBase issue)
    {
        // Finalizing an issue already out would clear its giver's next issue
        if (!IsInManager(issue)) return;

        try
        {
            Dismiss(issue);
        }
        finally
        {
            if (IsInManager(issue))
            {
                Logger.Warning("Finalizing {issue} failed, it is only deactivated so what its quest spawned can stay on the map", issue.StringId);
                // With its quest still attached, DeactivateIssue would cancel that quest again instead of removing the issue
                issue.IssueQuest = null;
                Campaign.Current.IssueManager.DeactivateIssue(issue);
            }
        }
    }

    public void ReturnTroops(IssueBase issue) => Campaign.Current.IssueManager.TryToMakeTroopsReturn(issue);

    public void AddTroopsReturnedLog(IssueBase issue)
    {
        if (!issue.JournalEntries.Any(log => log.LogText?.Value == ReturnedTroopsText))
            issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(ReturnedTroopsText)));
    }

    public void SendCancel(IssueBase issue) =>
        CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueCancel, Hero.MainHero);

    public void CloseJournal(IssueBase issue, IssueBase.IssueUpdateDetails ending)
    {
        var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.IsRelatedTo(issue));
        if (archive != null && !archive.IsEnded())
            archive.Update(archive.GetEntries().Union(issue.JournalEntries).ToMBList(), ending);
    }

    public void DropUnfinishedJournal(IssueBase issue) =>
        Campaign.Current.LogEntryHistory._logs.RemoveAll(
            entry => entry is JournalLogEntry journal && journal.IsRelatedTo(issue) && !journal.IsEnded());

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
