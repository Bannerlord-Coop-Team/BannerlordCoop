using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;
using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

internal interface IGangLeaderWeaponsOwnerContext
{
    bool TryOpen(Quest quest, out IDisposable scope);
    bool TryOpen(Issue issue, out IDisposable scope);
}

internal sealed class GangLeaderWeaponsOwnerContext : IGangLeaderWeaponsOwnerContext
{
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public GangLeaderWeaponsOwnerContext(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects)
    {
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
    }

    public bool TryOpen(Quest quest, out IDisposable scope)
        => TryOpen(quest.QuestGiver, quest, out scope);

    public bool TryOpen(Issue issue, out IDisposable scope)
        => TryOpen(issue.IssueOwner, null, out scope);

    private bool TryOpen(Hero giver, Quest quest, out IDisposable scope)
    {
        scope = null;
        if (!ownership.TryGetOwnerControllerId(giver, out var controllerId) ||
            !players.TryGetPlayer(controllerId, out var player) ||
            !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        var change = GangLeaderWeaponsPlayerChangeScope.Current;
        if (change != null)
        {
            if (hero != change.OldPlayer && hero != change.NewPlayer) return false;
            hero = change.NewPlayer;
            party = change.NewParty;
        }
        scope = new OwnerScope(quest, hero, party);
        return true;
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope player;
        private readonly GangLeaderWeaponsActionScope action;
        private readonly IssueFinalizeAuthorityGuard finalization;
        private readonly AlternativeSolutionCompletionAuthorityGuard alternative;

        public OwnerScope(Quest quest, Hero hero, MobileParty party)
        {
            player = new MainHeroSubstitutionScope(hero, party);
            action = new GangLeaderWeaponsActionScope(quest);
            finalization = new IssueFinalizeAuthorityGuard();
            if (quest == null) alternative = new AlternativeSolutionCompletionAuthorityGuard();
        }

        public void Dispose()
        {
            alternative?.Dispose();
            finalization.Dispose();
            action.Dispose();
            player.Dispose();
        }
    }
}

internal sealed class GangLeaderWeaponsPlayerChangeScope : IDisposable
{
    [ThreadStatic] private static GangLeaderWeaponsPlayerChangeScope current;
    private readonly GangLeaderWeaponsPlayerChangeScope previous;
    public static GangLeaderWeaponsPlayerChangeScope Current => current;
    public readonly Hero OldPlayer;
    public readonly Hero NewPlayer;
    public readonly MobileParty NewParty;

    public GangLeaderWeaponsPlayerChangeScope(Hero oldPlayer, Hero newPlayer, MobileParty newParty)
    {
        OldPlayer = oldPlayer;
        NewPlayer = newPlayer;
        NewParty = newParty;
        previous = current;
        current = this;
    }

    public void Dispose() => current = previous;
}
