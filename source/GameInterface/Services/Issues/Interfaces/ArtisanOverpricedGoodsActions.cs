using Common;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

public interface IArtisanOverpricedGoodsActions
{
    void Request(Hero giver, ArtisanOverpricedGoodsAction action);
    bool Apply(Player player, RequestArtisanOverpricedGoodsAction request);
    void CancelOwnedIssues(Player player, bool permanentRemoval);
}

internal sealed class ArtisanOverpricedGoodsActions : IArtisanOverpricedGoodsActions
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueConversationTracker conversations;
    private readonly IArtisanOverpricedGoodsIssueInterface issueInterface;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry returningTroops;
    private readonly IPlayerManager players;

    public ArtisanOverpricedGoodsActions(IObjectManager objectManager, INetwork network,
        IIssueOwnershipRegistry ownership, IIssueGenerationRegistry generations,
        IIssueConversationTracker conversations, IArtisanOverpricedGoodsIssueInterface issueInterface,
        IAwaitingAlternativeSolutionTroopsRegistry returningTroops, IPlayerManager players)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.ownership = ownership;
        this.generations = generations;
        this.conversations = conversations;
        this.issueInterface = issueInterface;
        this.returningTroops = returningTroops;
        this.players = players;
    }

    public void CancelOwnedIssues(Player player, bool permanentRemoval)
    {
        if (ModInformation.IsClient || player == null) return;
        var log = permanentRemoval
            ? new TextObject("{=!}The agreement was canceled because the adventurer left the campaign.")
            : new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\"");
        foreach (var entry in ownership.Snapshot())
        {
            if (entry.Value != player.ControllerId || entry.Key.Issue is not Issue issue) continue;
            if (issue.IssueQuest is Quest quest) quest.CompleteQuestWithCancel(log);
            else if (permanentRemoval) issue.CompleteIssueWithCancel(log);
        }

        if (!permanentRemoval || !returningTroops.TryGet(player.ControllerId, out var troops)) return;
        foreach (var element in troops.GetTroopRoster())
            if (element.Character.IsHero) element.Character.HeroObject.HeroState = Hero.CharacterStates.Active;
        if (objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
            party.MemberRoster.Add(troops);
        returningTroops.Clear(player.ControllerId);
        if (players.TryGetPeer(player.ControllerId, out var peer))
            network.Send(peer, new NetworkAwaitingAlternativeSolutionTroopsDrained());
    }

    public void Request(Hero giver, ArtisanOverpricedGoodsAction action)
    {
        if (ModInformation.IsServer || giver?.Issue is not Issue issue) return;
        if (action != ArtisanOverpricedGoodsAction.StartLordSolution && !ownership.IsLocalPeerOwner(giver)) return;
        if (!objectManager.TryGetIdWithLogging(giver, out var giverId)) return;
        if (!generations.TryGetGeneration(giver, out var generation)) return;
        network.SendAll(new RequestArtisanOverpricedGoodsAction(giverId, generation,
            (issue.IssueQuest as Quest)?._givenTradeGoods ?? 0, action));
    }

    public bool Apply(Player player, RequestArtisanOverpricedGoodsAction request)
    {
        if (ModInformation.IsClient || player == null) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(request.OwnerId, out var giver)) return false;
        if (giver.Issue is not Issue issue) return false;
        if (!generations.TryGetGeneration(giver, out var generation) || generation != request.Generation) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        if (party.CurrentSettlement == null || party.CurrentSettlement != giver.CurrentSettlement) return false;

        var startingLord = request.Action == ArtisanOverpricedGoodsAction.StartLordSolution;
        if (!startingLord && (!ownership.TryGetOwnerControllerId(giver, out var owner) || owner != player.ControllerId)) return false;
        using (new MainHeroSubstitutionScope(hero, party))
        {
            if (startingLord)
            {
                if (!issue.IsOngoingWithoutQuest || ownership.TryGetOwnerControllerId(giver, out _)) return false;
                if (!conversations.TryGetTrackedRequester(request.OwnerId, player.ControllerId, out var tracked) || tracked != generation) return false;
                if (!issue.CheckPreconditions(giver, out _) || !issue.LordSolutionCondition(out _)) return false;
                if (hero.Clan == null || hero.Clan.Influence < issue.NeededInfluenceForLordSolution) return false;
            }
            else if (!CanApply(issue, party, request)) return false;

            using (new ArtisanOverpricedGoodsActionGuard(giver, hero, request.Action))
            using (new IssueFinalizeAuthorityGuard())
            {
                Execute(issue, player, request);
            }
        }
        return true;
    }

    private static bool CanApply(Issue issue, MobileParty party, RequestArtisanOverpricedGoodsAction request)
    {
        if (request.Action == ArtisanOverpricedGoodsAction.AcceptLordOffer || request.Action == ArtisanOverpricedGoodsAction.RefuseLordOffer)
        {
            if (issue._issueState != IssueBase.IssueState.SolvingWithLordSolution) return false;
            return request.Action == ArtisanOverpricedGoodsAction.RefuseLordOffer || issue.CounterOfferHero?.IsActive == true;
        }
        if (issue.IssueQuest is not Quest quest || !quest.IsOngoing || quest.QuestDueTime.IsPast) return false;
        if (quest._givenTradeGoods != request.ExpectedDelivered) return false;
        var remaining = quest._requestedTradeGoodAmount - quest._givenTradeGoods;
        var available = party.ItemRoster.GetItemNumber(quest._requestedTradeGood);
        return request.Action switch
        {
            ArtisanOverpricedGoodsAction.DeliverPartial => remaining > 0 && available > 0 && available < remaining,
            ArtisanOverpricedGoodsAction.DeliverFull => remaining > 0 && available >= remaining,
            ArtisanOverpricedGoodsAction.AcceptMerchantOffer => quest.AntagonistHero?.IsActive == true,
            _ => false,
        };
    }

    private void Execute(Issue issue, Player player, RequestArtisanOverpricedGoodsAction request)
    {
        var quest = issue.IssueQuest as Quest;
        switch (request.Action)
        {
            case ArtisanOverpricedGoodsAction.DeliverPartial:
                quest.DeliverItemsPartiallyOnConsequence();
                Broadcast(issue, player, request);
                break;
            case ArtisanOverpricedGoodsAction.DeliverFull:
                quest.DeliverItemsFullyOnConsequence();
                Broadcast(issue, player, request);
                quest.CompleteQuestWithSuccess();
                break;
            case ArtisanOverpricedGoodsAction.AcceptMerchantOffer:
                quest.AcceptCounterOffer();
                break;
            case ArtisanOverpricedGoodsAction.StartLordSolution:
                ownership.SetOwner(issue.IssueOwner, player.ControllerId);
                issue.StartIssueWithLordSolution();
                Broadcast(issue, player, request);
                break;
            case ArtisanOverpricedGoodsAction.AcceptLordOffer:
                issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
                break;
            case ArtisanOverpricedGoodsAction.RefuseLordOffer:
                issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
                break;
        }
    }

    private void Broadcast(Issue issue, Player player, RequestArtisanOverpricedGoodsAction request)
    {
        network.SendAll(new NetworkArtisanOverpricedGoodsActionApplied(request.OwnerId, request.Generation,
            player.ControllerId, request.Action, (issue.IssueQuest as Quest)?._givenTradeGoods ?? 0,
            issueInterface.CaptureValues(issue)));
    }
}
