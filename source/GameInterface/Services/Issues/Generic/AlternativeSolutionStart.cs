using System;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Generic;

public sealed class AlternativeSolutionStartAuthorityGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public AlternativeSolutionStartAuthorityGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}

public static class AlternativeSolutionStartRunner
{
    public static AlternativeSolutionVanillaState StartOnServer(Hero owner, Player truePlayer, Func<bool> captureFields = null)
        => StartOnServer(owner.Issue, truePlayer, null, captureFields);

    public static AlternativeSolutionVanillaState StartOnServerFromClaim(
        Hero owner, Player truePlayer, TroopRoster validatedRoster, Func<bool> captureFields)
        => StartOnServer(owner.Issue, truePlayer, validatedRoster, captureFields);

    private static AlternativeSolutionVanillaState StartOnServer(
        IssueBase issue, Player truePlayer, TroopRoster validatedRoster, Func<bool> captureFields)
    {
        using (new AlternativeSolutionStartAuthorityGuard())
        using (ResolveOwnerScope(truePlayer))
        {
            var roster = validatedRoster ?? issue.AlternativeSolutionSentTroops;
            if (!issue.AlternativeSolutionCondition(out var explanation))
                throw new InvalidOperationException($"Alternative solution is no longer available for {issue.StringId}: {explanation}");
            if (validatedRoster != null && !IsCompanionAvailable(roster))
                throw new InvalidOperationException($"The selected companion is no longer available for {issue.StringId}");
            if (validatedRoster != null && !DoTroopsSatisfyAlternativeSolution(issue, roster, out explanation))
                throw new InvalidOperationException($"The selected troops no longer satisfy {issue.StringId}: {explanation}");

            var before = (issue._issueState, issue.IsTriedToSolveBefore, issue.IssueDueTime,
                issue._issueDifficultyMultiplier, issue.AlternativeSolutionReturnTimeForTroops,
                issue.AlternativeSolutionIssueEffectClearTime, issue._failureChance,
                issue._alternativeSolutionCasualtyCount, issue._totalTroopXpAmount, issue._companionRewardSkill);
            var logCount = issue.JournalEntries.Count;
            var history = Campaign.Current.LogEntryHistory;
            var previousLog = history.FindLastGameActionLog<JournalLogEntry>(log => log.IsRelatedTo(issue));
            var previousLogStatus = previousLog?._lastIssueStatus;
            var heroes = roster.GetTroopRoster().Where(element => element.Character.IsHero)
                .ToDictionary(element => element.Character.HeroObject, element => element.Character.HeroObject.HeroState);
            try
            {
                if (validatedRoster != null)
                {
                    issue.AlternativeSolutionSentTroops.Clear();
                    RemoveFromTrueOwnerParty(issue, validatedRoster);
                    issue.AlternativeSolutionStartConsequence();
                }
                issue.StartIssueWithAlternativeSolution();
                if (captureFields?.Invoke() == false)
                    throw new InvalidOperationException($"Could not capture alternative acceptance for {issue.StringId}");
                return AlternativeSolutionVanillaStateSync.Capture(issue);
            }
            catch
            {
                foreach (var hero in heroes)
                    if (hero.Key.HeroState != hero.Value) hero.Key.ChangeState(hero.Value);
                MobileParty.MainParty.MemberRoster.Add(issue.AlternativeSolutionSentTroops);
                issue.AlternativeSolutionSentTroops.Clear();
                (issue._issueState, issue.IsTriedToSolveBefore, issue.IssueDueTime,
                    issue._issueDifficultyMultiplier, issue.AlternativeSolutionReturnTimeForTroops,
                    issue.AlternativeSolutionIssueEffectClearTime, issue._failureChance,
                    issue._alternativeSolutionCasualtyCount, issue._totalTroopXpAmount, issue._companionRewardSkill) = before;
                issue._journalEntries.RemoveRange(logCount, issue.JournalEntries.Count - logCount);
                for (var i = history.GameActionLogs.Count - 1; i >= 0; i--)
                    if (history.GameActionLogs[i] is JournalLogEntry log && log != previousLog && log.IsRelatedTo(issue))
                        history.DeleteLogAtIndex(i);
                if (previousLog != null)
                {
                    previousLog.Update(issue.JournalEntries);
                    previousLog._lastIssueStatus = previousLogStatus.Value;
                }
                QuestTypeRegistry.Get(issue)?.RejectAlternativeAccept?.Invoke(issue.IssueOwner);
                throw;
            }
        }
    }

    private static bool IsCompanionAvailable(TroopRoster roster)
    {
        if (roster.TotalHeroes != 1) return false;
        var companion = roster.GetTroopRoster().First(element => element.Character.IsHero).Character.HeroObject;
        return companion != Hero.MainHero && companion.PartyBelongedTo == MobileParty.MainParty
            && companion.CanHaveCampaignIssues() && !companion.IsWounded && !companion.IsPregnant;
    }

    private static bool DoTroopsSatisfyAlternativeSolution(IssueBase issue, TroopRoster troopRoster, out TextObject explanation)
    {
        var neededMenCount = issue.GetTotalAlternativeSolutionNeededMenCount();
        if (troopRoster.TotalRegulars >= neededMenCount && troopRoster.TotalRegulars - troopRoster.TotalWoundedRegulars < neededMenCount)
        {
            explanation = new TextObject("{=fjmGXcLW}You have to send healthy troops to this quest.");
            return false;
        }
        return issue.DoTroopsSatisfyAlternativeSolution(troopRoster, out explanation);
    }

    private static void RemoveFromTrueOwnerParty(IssueBase issue, TroopRoster validatedRoster)
    {
        foreach (var element in validatedRoster.GetTroopRoster())
        {
            MobileParty.MainParty.MemberRoster.AddToCounts(
                element.Character, -element.Number, false, -element.WoundedNumber, 0, true, -1);
            issue.AlternativeSolutionSentTroops.AddToCounts(
                element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
            if (element.Character.IsHero)
                CampaignEventDispatcher.Instance.OnHeroGetsBusy(element.Character.HeroObject, HeroGetsBusyReasons.SolvesIssue);
        }
    }

    private static MainHeroSubstitutionScope ResolveOwnerScope(Player truePlayer)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            throw new InvalidOperationException("ResolveOwnerScope: IObjectManager is not resolvable");
        if (!objectManager.TryGetObjectWithLogging<Hero>(truePlayer.HeroId, out var trueOwnerHero))
            throw new InvalidOperationException($"ResolveOwnerScope: could not resolve true owner Hero {truePlayer.HeroId}");

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(truePlayer.MobilePartyId, out var trueOwnerParty))
            throw new InvalidOperationException($"ResolveOwnerScope: could not resolve true owner party {truePlayer.MobilePartyId}");

        return new MainHeroSubstitutionScope(trueOwnerHero, trueOwnerParty);
    }
}
