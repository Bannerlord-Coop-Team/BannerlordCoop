using System;
using GameInterface.Services.Heroes.Patches;
using System.Linq;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Generic;

internal readonly struct AlternativeSolutionStartSnapshot
{
    private readonly IssueBase issue;
    private readonly AlternativeSolutionVanillaState state;
    private readonly CampaignTime dueTime;
    private readonly float difficulty;
    private readonly bool tried;
    private readonly SkillObject rewardSkill;
    private readonly JournalLog[] journal;

    public AlternativeSolutionStartSnapshot(IssueBase issue)
    {
        this.issue = issue;
        state = AlternativeSolutionVanillaStateSync.Capture(issue);
        dueTime = issue.IssueDueTime;
        difficulty = issue._issueDifficultyMultiplier;
        tried = issue.IsTriedToSolveBefore;
        rewardSkill = issue._companionRewardSkill;
        journal = issue.JournalEntries.ToArray();
    }

    public void Restore()
    {
        issue._issueState = IssueBase.IssueState.Ongoing;
        issue.IssueDueTime = dueTime;
        issue._issueDifficultyMultiplier = difficulty;
        issue.IsTriedToSolveBefore = tried;
        issue.AlternativeSolutionReturnTimeForTroops = state.ReturnTime;
        issue.AlternativeSolutionIssueEffectClearTime = state.EffectClearTime;
        issue._failureChance = state.FailureChance;
        issue._alternativeSolutionCasualtyCount = state.CasualtyCount;
        issue._totalTroopXpAmount = state.TotalTroopXpAmount;
        issue._companionRewardSkill = rewardSkill;
        issue._journalEntries.Clear();
        issue._journalEntries.AddRange(journal);
    }
}

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
    public static AlternativeSolutionVanillaState StartOnServer(Hero owner, Player truePlayer)
    {
        using (new AlternativeSolutionStartAuthorityGuard())
        using (ResolveOwnerScope(truePlayer))
        {
            if (!owner.Issue.AlternativeSolutionCondition(out _))
                throw new InvalidOperationException($"StartOnServer: AlternativeSolutionCondition rejected the accept for owner {owner.StringId}");

            owner.Issue.StartIssueWithAlternativeSolution();
            return AlternativeSolutionVanillaStateSync.Capture(owner.Issue);
        }
    }

    public static AlternativeSolutionVanillaState StartOnServerFromClaim(Hero owner, Player truePlayer, TroopRoster validatedRoster)
    {
        using (new AlternativeSolutionStartAuthorityGuard())
        using (ResolveOwnerScope(truePlayer))
        {
            if (!owner.Issue.AlternativeSolutionCondition(out _))
                throw new InvalidOperationException($"StartOnServerFromClaim: AlternativeSolutionCondition rejected the accept for owner {owner.StringId}");
            if (!DoTroopsSatisfyAlternativeSolution(owner.Issue, validatedRoster, out _))
                throw new InvalidOperationException($"StartOnServerFromClaim: the validated roster does not satisfy the alternative solution requirement for owner {owner.StringId}");
            if (owner.Issue is ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue &&
                !IsExtortionSelectionValid(owner.Issue, validatedRoster))
                throw new InvalidOperationException("The selected deserter mission companion or troops are no longer eligible.");

            RemoveFromTrueOwnerParty(validatedRoster);

            owner.Issue.AlternativeSolutionSentTroops.Clear();
            foreach (var element in validatedRoster.GetTroopRoster())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(
                    element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
            }

            owner.Issue.AlternativeSolutionStartConsequence();
            owner.Issue.StartIssueWithAlternativeSolution();
            return AlternativeSolutionVanillaStateSync.Capture(owner.Issue);
        }
    }

    internal static bool DoTroopsSatisfyAlternativeSolution(IssueBase issue, TroopRoster troopRoster, out TextObject explanation)
    {
        var neededMenCount = issue.GetTotalAlternativeSolutionNeededMenCount();
        if (troopRoster.TotalRegulars >= neededMenCount && troopRoster.TotalRegulars - troopRoster.TotalWoundedRegulars < neededMenCount)
        {
            explanation = new TextObject("{=fjmGXcLW}You have to send healthy troops to this quest.");
            return false;
        }
        return issue.DoTroopsSatisfyAlternativeSolution(troopRoster, out explanation);
    }

    private static bool IsExtortionSelectionValid(IssueBase issue, TroopRoster roster)
    {
        if (roster.TotalHeroes != 1) return false;
        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character.IsHero)
            {
                var companion = element.Character.HeroObject;
                if (companion == Hero.MainHero || companion.PartyBelongedTo != MobileParty.MainParty ||
                    !companion.CanHaveCampaignIssues() || companion.IsWounded || companion.IsPregnant) return false;
            }
            else if (element.Character.IsNotTransferableInPartyScreen ||
                !issue.IsTroopTypeNeededByAlternativeSolution(element.Character)) return false;
        }
        return true;
    }

    private static void RemoveFromTrueOwnerParty(TroopRoster validatedRoster)
    {
        var party = Campaign.Current?.MainParty;
        if (party == null) return;

        foreach (var element in validatedRoster.GetTroopRoster())
        {
            party.MemberRoster.AddToCounts(
                element.Character, -element.Number, false, -element.WoundedNumber, -element.Xp, true, -1);
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
