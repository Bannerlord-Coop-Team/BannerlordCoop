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
using TaleWorlds.CampaignSystem.LogEntries;
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

    [ThreadStatic]
    private static IssueBase orphanedRemovalIssue;

    [ThreadStatic]
    private static MobileParty orphanedRemovalParty;

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
        if (orphanedRemovalIssue?.IssueOwner == giver && orphanedRemovalIssue != null)
        {
            scope = new IssueFinalizeAuthorityGuard();
            return true;
        }
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
        Hero hero = null;
        MobileParty party = null;
        if (players.TryGetPlayer(controllerId, out var player))
        {
            objects.TryGetObject(player.HeroId, out hero);
            objects.TryGetObject(player.MobilePartyId, out party);
        }

        using (hero != null && party != null ? new OwnerScope(hero, party) : null)
        using (new IssueFinalizeAuthorityGuard())
        {
            var log = new TextObject("{=coop_herd_player_removed}The quest was canceled because its player left the campaign.");
            foreach (var issue in issues)
            {
                var previousIssue = orphanedRemovalIssue;
                var previousParty = orphanedRemovalParty;
                try
                {
                    if (hero == null || party == null)
                    {
                        orphanedRemovalIssue = issue;
                        orphanedRemovalParty = party;
                    }
                    if (issue.IssueQuest?.IsOngoing == true) issue.IssueQuest.CompleteQuestWithCancel(log);
                    else if (issue.IsSolvingWithAlternative) issue.CompleteIssueWithCancel(log);
                }
                finally
                {
                    orphanedRemovalIssue = previousIssue;
                    orphanedRemovalParty = previousParty;
                }
            }
            if (awaitingTroops.TryGet(controllerId, out var troops))
            {
                // Release companions before the player's party and registration are removed.
                if (hero != null && party != null)
                    Campaign.Current.IssueManager.MakeAlternativeTroopsReturn(troops);
                else
                {
                    foreach (var element in troops.GetTroopRoster())
                        if (element.Character.IsHero) element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
                    party?.MemberRoster.Add(troops);
                }
                awaitingTroops.Clear(controllerId);
                if (players.TryGetPeer(controllerId, out var peer))
                    network.Send(peer, new NetworkAwaitingAlternativeSolutionTroopsDrainResult(
                        new TroopRosterData(Array.Empty<TroopRosterElementData>())));
            }
        }
    }

    internal static bool CancelOrphanedHerd(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest quest)
    {
        if (orphanedRemovalIssue?.IssueQuest != quest) return false;
        if (orphanedRemovalParty != null)
        {
            var available = orphanedRemovalParty.ItemRoster
                .Where(item => item.EquipmentElement.Item == quest._herdTypeToDeliver).Sum(item => item.Amount);
            orphanedRemovalParty.ItemRoster.AddToCounts(quest._herdTypeToDeliver, -Math.Min(available, quest._animalCountToDeliver));
        }
        return true;
    }

    internal static bool CompleteOrphanedCancellation(IssueBase issue, TextObject log)
    {
        if (!ReferenceEquals(orphanedRemovalIssue, issue)) return false;
        if (issue.IssueQuest?.IsOngoing == true)
        {
            issue.IssueQuest.CompleteQuestWithCancel(log);
            return true;
        }
        var alternative = issue.IssueQuest == null && issue.IsSolvingWithAlternative;
        if (alternative)
        {
            issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("{=V5Za6d4h}Your troops have returned from their mission.")));
            Campaign.Current.IssueManager.TryToMakeTroopsReturn(issue);
        }
        // IssueBase's cancellation reads MainHero even when that player's hero no longer exists.
        CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueCancel, null);
        if (alternative)
            Campaign.Current.LogEntryHistory.FindLastGameActionLog((JournalLogEntry entry) => entry.IsRelatedTo(issue))
                ?.Update(issue.JournalEntries, IssueBase.IssueUpdateDetails.IssueCancel);
        issue.IssueFinalized();
        return true;
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
