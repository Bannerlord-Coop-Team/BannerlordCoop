using System;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

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
            owner.Issue.StartIssueWithAlternativeSolution();
            return AlternativeSolutionVanillaStateSync.Capture(owner.Issue);
        }
    }

    private static IDisposable ResolveOwnerScope(Player truePlayer)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return NullScope.Instance;
        if (!objectManager.TryGetObjectWithLogging<Hero>(truePlayer.HeroId, out var trueOwnerHero)) return NullScope.Instance;

        objectManager.TryGetObjectWithLogging<MobileParty>(truePlayer.MobilePartyId, out var trueOwnerParty);

        return new MainHeroSubstitutionScope(trueOwnerHero, trueOwnerParty);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}
