using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Common;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues;

internal interface ISmugglersQuestAuthority
{
    bool TryOpenOwnerScope(Hero issueGiver, out IDisposable scope);
    bool IsLocalOwner(QuestBase quest);
    void OnPlayerReplaced(Player previous, Player replacement);
}

internal sealed class SmugglersQuestAuthority : ISmugglersQuestAuthority
{
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly ISmugglersQuestOwners owners;

    public SmugglersQuestAuthority(IIssueOwnershipRegistry ownershipRegistry,
        IPlayerManager playerManager, IObjectManager objectManager, ISmugglersQuestOwners owners)
    {
        this.ownershipRegistry = ownershipRegistry;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.owners = owners;
    }

    public bool IsLocalOwner(QuestBase quest) => ownershipRegistry.IsLocalPeerOwner(quest.QuestGiver);

    public void OnPlayerReplaced(Player previous, Player replacement)
    {
        if (previous.HeroId == replacement.HeroId) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(previous.HeroId, out var oldHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(replacement.HeroId, out var newHero)) return;
        if (ModInformation.IsServer)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(replacement.MobilePartyId, out var party)) return;
            using (new OwnerScope(oldHero, party))
            {
                foreach (var entry in owners.Snapshot())
                {
                    if (entry.Value != oldHero || entry.Key is not QuestBase quest || !quest.IsOngoing) continue;
                    quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
                }
            }
        }
        owners.ReplacePlayer(oldHero, newHero);
    }

    public bool TryOpenOwnerScope(Hero issueGiver, out IDisposable scope)
    {
        scope = null;
        if (!ownershipRegistry.TryGetOwnerControllerId(issueGiver, out var controllerId)) return false;
        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;

        scope = new OwnerScope(hero, party);
        return true;
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope playerScope;
        private readonly IssueFinalizeAuthorityGuard finalizeScope;

        internal OwnerScope(Hero hero, MobileParty party)
        {
            playerScope = new MainHeroSubstitutionScope(hero, party);
            finalizeScope = new IssueFinalizeAuthorityGuard();
        }

        public void Dispose()
        {
            finalizeScope.Dispose();
            playerScope.Dispose();
        }
    }
}
