using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Common;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues;

internal interface ISmugglersQuestAuthority
{
    bool TryOpenOwnerScope(Hero issueGiver, out IDisposable scope);
    bool IsLocalOwner(QuestBase quest);
    bool IsRemovedOwner(IssueBase issue);
    void OnPlayerReplaced(Player previous, Player replacement);
    void OnPlayerRemoved(Player player);
}

internal sealed class SmugglersQuestAuthority : ISmugglersQuestAuthority
{
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly ISmugglersQuestOwners owners;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry returningTroops;
    private readonly INetwork network;

    public SmugglersQuestAuthority(IIssueOwnershipRegistry ownershipRegistry,
        IPlayerManager playerManager, IObjectManager objectManager, ISmugglersQuestOwners owners,
        IAwaitingAlternativeSolutionTroopsRegistry returningTroops, INetwork network)
    {
        this.ownershipRegistry = ownershipRegistry;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.owners = owners;
        this.returningTroops = returningTroops;
        this.network = network;
    }

    public bool IsLocalOwner(QuestBase quest) => ownershipRegistry.IsLocalPeerOwner(quest.QuestGiver);

    public bool IsRemovedOwner(IssueBase issue) =>
        ownershipRegistry.TryGetOwnerControllerId(issue.IssueOwner, out var controller)
        && !playerManager.TryGetPlayer(controller, out _);

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

    public void OnPlayerRemoved(Player player)
    {
        if (ModInformation.IsClient) return;
        var issues = Campaign.Current.IssueManager.Issues.Values
            .Where(issue => issue is SmugglersIssueBehavior.SmugglersIssue
                && ownershipRegistry.TryGetOwnerControllerId(issue.IssueOwner, out var controller)
                && controller == player.ControllerId).ToArray();
        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
        if (issues.Length > 0)
        {
            objectManager.TryGetObject<Hero>(player.HeroId, out var hero);
            using (IDisposable scope = hero != null ? new OwnerScope(hero, party) : new IssueFinalizeAuthorityGuard())
            {
                foreach (var issue in issues)
                {
                    if (issue.IssueQuest is { IsOngoing: true } quest)
                        quest.CompleteQuestWithCancel(new TextObject("{=CoopSmugglersPlayerRemoved}The quest was canceled because the accepting character was removed."));
                    else
                        issue.CompleteIssueWithCancel();
                }
            }
        }
        // Return survivors to the old character before deletion can reuse this controller id.
        if (returningTroops.TryGet(player.ControllerId, out var troops))
            IssueManagerAlternativeSolutionTroopsPatches.MakeAlternativeTroopsReturn(troops, party);
        returningTroops.Clear(player.ControllerId);
        network.SendAll(new NetworkQuestPlayerRemoved(player.ControllerId, player.HeroId));
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
