using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues;

internal interface IExtortionQuestContext
{
    bool TryGetPlayer(Hero giver, out Hero playerHero, out MobileParty playerParty);
    bool TryEnter(Hero giver, out IDisposable scope);
    bool HasActiveIssue(Hero playerHero);
}

internal sealed class ExtortionQuestContext : IExtortionQuestContext
{
    [ThreadStatic] private static int activeScopes;
    internal static bool IsActive => activeScopes > 0;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public ExtortionQuestContext(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects)
    {
        if (ownership == null) throw new ArgumentNullException(nameof(ownership));
        if (players == null) throw new ArgumentNullException(nameof(players));
        if (objects == null) throw new ArgumentNullException(nameof(objects));
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
    }

    public bool TryGetPlayer(Hero giver, out Hero playerHero, out MobileParty playerParty)
    {
        playerHero = null;
        playerParty = null;
        if (!ownership.TryGetOwnerControllerId(giver, out var controllerId) ||
            !players.TryGetPlayer(controllerId, out var player)) return false;
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;

        playerHero = hero;
        playerParty = party;
        return true;
    }

    public bool TryEnter(Hero giver, out IDisposable scope)
    {
        scope = null;
        if (!TryGetPlayer(giver, out var hero, out var party)) return false;
        scope = new ExecutionScope(hero, party);
        return true;
    }

    public bool HasActiveIssue(Hero playerHero)
    {
        if (playerHero == null) return false;
        foreach (var entry in ownership.Snapshot())
        {
            if (entry.Key.Issue is not ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue issue ||
                !(issue.IsSolvingWithQuest || issue.IsSolvingWithAlternative)) continue;
            if (players.TryGetPlayer(entry.Value, out var player) &&
                objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) && hero == playerHero) return true;
        }
        return false;
    }

    private sealed class ExecutionScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope player;
        private readonly IssueFinalizeAuthorityGuard authority;
        private bool disposed;

        public ExecutionScope(Hero hero, MobileParty party)
        {
            player = new MainHeroSubstitutionScope(hero, party);
            authority = new IssueFinalizeAuthorityGuard();
            activeScopes++;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            authority.Dispose();
            player.Dispose();
            activeScopes--;
        }
    }
}
