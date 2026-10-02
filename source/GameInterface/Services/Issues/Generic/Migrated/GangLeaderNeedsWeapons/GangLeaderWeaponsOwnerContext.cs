using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal interface IGangLeaderWeaponsOwnerContext
{
    bool TryOpen(Quest quest, out IDisposable scope);
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
    {
        scope = null;
        if (!ownership.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) ||
            !players.TryGetPlayer(controllerId, out var player) ||
            !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        scope = new OwnerScope(quest, hero, party);
        return true;
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope player;
        private readonly GangLeaderWeaponsActionScope action;
        private readonly IssueFinalizeAuthorityGuard finalization;

        public OwnerScope(Quest quest, Hero hero, MobileParty party)
        {
            player = new MainHeroSubstitutionScope(hero, party);
            action = new GangLeaderWeaponsActionScope(quest);
            finalization = new IssueFinalizeAuthorityGuard();
        }

        public void Dispose()
        {
            finalization.Dispose();
            action.Dispose();
            player.Dispose();
        }
    }
}
