using System;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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

    private static void RemoveFromTrueOwnerParty(TroopRoster validatedRoster)
    {
        var party = Campaign.Current?.MainParty;
        if (party == null) return;

        foreach (var element in validatedRoster.GetTroopRoster())
        {
            party.MemberRoster.AddToCounts(
                element.Character, -element.Number, false, -element.WoundedNumber, 0, true, -1);
        }
    }

    private static MainHeroSubstitutionScope ResolveOwnerScope(Player truePlayer)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            throw new InvalidOperationException("ResolveOwnerScope: IObjectManager is not resolvable");
        if (!objectManager.TryGetObjectWithLogging<Hero>(truePlayer.HeroId, out var trueOwnerHero))
            throw new InvalidOperationException($"ResolveOwnerScope: could not resolve true owner Hero {truePlayer.HeroId}");

        objectManager.TryGetObjectWithLogging<MobileParty>(truePlayer.MobilePartyId, out var trueOwnerParty);

        return new MainHeroSubstitutionScope(trueOwnerHero, trueOwnerParty);
    }
}
