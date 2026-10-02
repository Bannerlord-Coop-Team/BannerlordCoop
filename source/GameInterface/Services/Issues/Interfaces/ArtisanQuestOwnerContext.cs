using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

public interface IArtisanQuestOwnerContext
{
    bool IsLocalOwner(Hero giver);
    bool IsOwnedBy(Hero giver, Hero player);
    bool HasConflictingIssue(Hero player);
    bool TryEnter(Hero giver, out IDisposable scope);
}

internal sealed class ArtisanQuestOwnerContext : IArtisanQuestOwnerContext
{
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public ArtisanQuestOwnerContext(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects)
    {
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
    }

    public bool IsLocalOwner(Hero giver) => Scope.Current != null && Scope.Current.Giver == giver
        ? Scope.Current.LocalOwner
        : ownership.IsLocalPeerOwner(giver);

    public bool IsOwnedBy(Hero giver, Hero player)
    {
        return player != null && ownership.TryGetOwnerControllerId(giver, out var controller) &&
            players.TryGetPlayer(controller, out var owner) &&
            objects.TryGetObjectWithLogging<Hero>(owner.HeroId, out var hero) && hero == player;
    }

    public bool HasConflictingIssue(Hero player)
    {
        foreach (var entry in Campaign.Current.IssueManager.Issues)
        {
            if (entry.Value is Issue issue && (issue.IsSolvingWithQuest || issue.IsSolvingWithAlternative) &&
                IsOwnedBy(entry.Key, player)) return true;
        }
        return false;
    }

    public bool TryEnter(Hero giver, out IDisposable scope)
    {
        scope = null;
        if (!ownership.TryGetOwnerControllerId(giver, out var controller) || !players.TryGetPlayer(controller, out var player)) return false;
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        scope = new Scope(giver, hero, party, IsLocalOwner(giver));
        return true;
    }

    private sealed class Scope : IDisposable
    {
        [ThreadStatic]
        public static Scope Current;

        public readonly Hero Giver;
        public readonly bool LocalOwner;
        private readonly Scope previous;
        private readonly MainHeroSubstitutionScope playerScope;
        private readonly IssueFinalizeAuthorityGuard authority;

        public Scope(Hero giver, Hero player, MobileParty party, bool localOwner)
        {
            previous = Current;
            Giver = giver;
            LocalOwner = localOwner;
            playerScope = new MainHeroSubstitutionScope(player, party);
            authority = new IssueFinalizeAuthorityGuard();
            Current = this;
        }

        public void Dispose()
        {
            Current = previous;
            authority.Dispose();
            playerScope.Dispose();
        }
    }
}
