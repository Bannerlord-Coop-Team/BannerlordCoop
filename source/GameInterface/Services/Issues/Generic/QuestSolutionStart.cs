using System;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

public sealed class QuestSolutionStartAuthorityGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public QuestSolutionStartAuthorityGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}

public static class QuestSolutionStartRunner
{
    public static T RunGuarded<T>(Player truePlayer, Func<T> action)
    {
        using (new QuestSolutionStartAuthorityGuard())
        using (ResolveOwnerScope(truePlayer))
        {
            return action();
        }
    }

    public static bool StartOnServer(Hero owner, Player truePlayer)
        => RunGuarded(truePlayer, () => owner.Issue.StartIssueWithQuest());

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
