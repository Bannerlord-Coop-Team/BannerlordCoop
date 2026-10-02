using Common.Network;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

public interface IHeadmanHerdQuestAuthority
{
    bool TryEnter(Hero giver, out IDisposable scope);
    void CancelForPlayerChange(string controllerId);
    void CancelForPlayerRemoval(string controllerId);
}

internal sealed class HeadmanHerdQuestAuthority : IHeadmanHerdQuestAuthority
{
    [ThreadStatic]
    private static Hero traitOwner;

    internal static Hero TraitOwner => traitOwner;

    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry awaitingTroops;
    private readonly INetwork network;

    public HeadmanHerdQuestAuthority(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects,
        IAwaitingAlternativeSolutionTroopsRegistry awaitingTroops, INetwork network)
    {
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
        this.awaitingTroops = awaitingTroops;
        this.network = network;
    }

    public bool TryEnter(Hero giver, out IDisposable scope)
    {
        scope = null;
        if (!ownership.TryGetOwnerControllerId(giver, out var controllerId)) return false;
        if (!players.TryGetPlayer(controllerId, out var player)) return false;
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        scope = new OwnerScope(hero, party);
        return true;
    }

    public void CancelForPlayerChange(string controllerId)
    {
        var quests = Campaign.Current.QuestManager.Quests
            .OfType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>().ToArray();
        foreach (var quest in quests)
            if (quest.IsOngoing && ownership.TryGetOwnerControllerId(quest.QuestGiver, out var owner) && owner == controllerId)
                quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
    }

    public void CancelForPlayerRemoval(string controllerId)
    {
        var issues = Campaign.Current.IssueManager.Issues.Values
            .OfType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>()
            .Where(issue => ownership.TryGetOwnerControllerId(issue.IssueOwner, out var owner) && owner == controllerId).ToArray();
        if (issues.Length == 0 && !awaitingTroops.TryGet(controllerId, out _)) return;
        if (!players.TryGetPlayer(controllerId, out var player)
            || !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)
            || !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
            throw new InvalidOperationException("Cannot clear a player's herd quest without its registered hero and party");

        using (new OwnerScope(hero, party))
        {
            var log = new TextObject("{=coop_herd_player_removed}The quest was canceled because its player left the campaign.");
            foreach (var issue in issues)
            {
                if (issue.IssueQuest?.IsOngoing == true) issue.IssueQuest.CompleteQuestWithCancel(log);
                else if (issue.IsSolvingWithAlternative) issue.CompleteIssueWithCancel(log);
            }
            if (awaitingTroops.TryGet(controllerId, out var troops))
            {
                // Release companions before the player's party and registration are removed.
                Campaign.Current.IssueManager.MakeAlternativeTroopsReturn(troops);
                awaitingTroops.Clear(controllerId);
                if (players.TryGetPeer(controllerId, out var peer))
                    network.Send(peer, new NetworkAwaitingAlternativeSolutionTroopsDrainResult(
                        new TroopRosterData(Array.Empty<TroopRosterElementData>())));
            }
        }
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly Hero previousTraitOwner;
        private readonly MainHeroSubstitutionScope playerScope;
        private readonly IssueFinalizeAuthorityGuard finalizationGuard;

        public OwnerScope(Hero hero, MobileParty party)
        {
            previousTraitOwner = traitOwner;
            playerScope = new MainHeroSubstitutionScope(hero, party);
            traitOwner = hero;
            finalizationGuard = new IssueFinalizeAuthorityGuard();
        }

        public void Dispose()
        {
            traitOwner = previousTraitOwner;
            finalizationGuard.Dispose();
            playerScope.Dispose();
        }
    }
}
