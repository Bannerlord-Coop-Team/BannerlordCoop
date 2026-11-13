using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Participation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Common.Network;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushEvents
{
    void Run(Action<Quest> action);
    void BattleEnded(Quest quest, MapEvent battle);
    void HoldCaravanForReward(Quest quest);
    void Release(Quest quest);
    void OpenOwnerConversation(Quest quest);
}

internal class CaravanAmbushEvents : ICaravanAmbushEvents
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly ICaravanAmbushStatePublisher publisher;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;
    private readonly IRetreatedMapEventPartyTracker retreatedPartyTracker;

    public CaravanAmbushEvents(IOutcomeDispatcher dispatcher, ICaravanAmbushStatePublisher publisher,
        INetwork network, IObjectManager objects, IIssueOwnerResolver owners, IPlayerManager players,
        IRetreatedMapEventPartyTracker retreatedPartyTracker)
    {
        this.dispatcher = dispatcher;
        this.publisher = publisher;
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.players = players;
        this.retreatedPartyTracker = retreatedPartyTracker;
    }

    public void Run(Action<Quest> action)
    {
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            action(quest);
            if (quest.IsOngoing)
                publisher.Publish(quest);
        });
    }

    public void BattleEnded(Quest quest, MapEvent battle)
    {
        if (quest._caravanParty == null || quest._banditParty == null ||
            battle.WinningSide == BattleSideEnum.None || battle.DefeatedSide == BattleSideEnum.None) return;
        var winners = battle.GetMapEventSide(battle.WinningSide);
        var losers = battle.GetMapEventSide(battle.DefeatedSide);
        if (losers.Parties.Any(party => party.Party == quest._caravanParty.Party))
        {
            HoldCaravanForReward(quest);
            quest.OnCaravanDestroyed(winners.LeaderParty);
            return;
        }
        if (!losers.Parties.Any(party => party.Party == quest._banditParty.Party)) return;
        if (quest._isCaravanSaved) return;
        quest._isCaravanSaved = true;
        HoldCaravanForReward(quest);
        var ownerParty = MobileParty.MainParty;
        // A retreated party stays in the winners list until the event is finalized.
        if (ownerParty == null || retreatedPartyTracker.IsRetreated(battle, ownerParty.Party) ||
            !winners.Parties.Any(party => party.Party == ownerParty.Party))
        {
            quest.OnCaravanSurvivedWithoutHelp();
        }
        else if (!quest._caravanParty.IsActive || !winners.Parties.Any(party => party.Party == quest._caravanParty.Party))
        {
            quest.OnQuestSucceeded();
        }
        else
        {
            OpenOwnerConversation(quest);
        }
    }

    public void OpenOwnerConversation(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || quest._caravanParty == null ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !objects.TryGetIdWithLogging(quest._caravanParty, out var caravanId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer)) return;
        network.Send(peer, new NetworkCaravanAmbushConversation(ownerId, issue.StringId, controllerId, caravanId));
    }

    public void HoldCaravanForReward(Quest quest)
    {
        if (quest._isCaravanSaved && quest._caravanParty?.IsActive == true)
        {
            // Keep the caravan available for the owner's reward conversation.
            quest._caravanParty.SetMoveGoToPoint(quest._caravanParty.Position, MobileParty.NavigationType.Default);
            quest._caravanParty.Ai.SetDoNotMakeNewDecisions(true);
        }
        else
        {
            ReleaseParty(quest._caravanParty, quest._targetSettlement);
        }
        ReleaseParty(quest._banditParty, quest._banditParty?.HomeSettlement);
    }

    public void Release(Quest quest)
    {
        ReleaseParty(quest._caravanParty, quest._targetSettlement);
        ReleaseParty(quest._banditParty, quest._banditParty?.HomeSettlement);
    }

    private static void ReleaseParty(MobileParty party, TaleWorlds.CampaignSystem.Settlements.Settlement destination)
    {
        if (party == null) return;
        if (party.MapEvent != null) party.MapEvent.IsInvulnerable = false;
        party.SetPartyUsedByQuest(false);
        if (!party.IsActive) return;
        party.Ai.SetDoNotMakeNewDecisions(false);
        if (destination != null)
        {
            SetPartyAiAction.GetActionForVisitingSettlement(party, destination, MobileParty.NavigationType.Default, false, false);
        }
    }
}
