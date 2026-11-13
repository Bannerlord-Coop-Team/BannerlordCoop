using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Participation;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushEvents
{
    void Run(Action<Quest> action);
    void BattleEnded(MapEvent battle);
    void End(Quest quest, bool publishFinalState);
    void Release(Quest quest);
}

internal class CaravanAmbushEvents : ICaravanAmbushEvents
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly ICaravanAmbushProgress publisher;
    private readonly IRetreatedMapEventPartyTracker retreatedPartyTracker;

    public CaravanAmbushEvents(IOutcomeDispatcher dispatcher, ICaravanAmbushProgress publisher,
        IRetreatedMapEventPartyTracker retreatedPartyTracker)
    {
        this.dispatcher = dispatcher;
        this.publisher = publisher;
        this.retreatedPartyTracker = retreatedPartyTracker;
    }

    public void Run(Action<Quest> action)
    {
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            var before = (quest._caravanParty, quest._banditParty, quest._isCaravanSaved,
                quest._vicinityCheckFailCounter, quest.JournalEntries.Count);
            action(quest);
            var after = (quest._caravanParty, quest._banditParty, quest._isCaravanSaved,
                quest._vicinityCheckFailCounter, quest.JournalEntries.Count);
            if (quest.IsOngoing && before != after)
                publisher.Publish(quest);
        });
    }

    public void End(Quest quest, bool publishFinalState)
    {
        if (publishFinalState) publisher.Publish(quest);
        quest.RemoveAllTrackedObjects();
        Release(quest);
    }

    public void BattleEnded(MapEvent battle) => Run(quest => BattleEnded(quest, battle));

    private void BattleEnded(Quest quest, MapEvent battle)
    {
        if (quest._caravanParty == null || quest._banditParty == null ||
            battle.WinningSide == BattleSideEnum.None || battle.DefeatedSide == BattleSideEnum.None) return;
        var winners = battle.GetMapEventSide(battle.WinningSide);
        var losers = battle.GetMapEventSide(battle.DefeatedSide);
        bool OnSide(MapEventSide side, MobileParty party) => side.Parties.Any(entry => entry.Party == party.Party);
        if (OnSide(losers, quest._caravanParty))
        {
            HoldCaravanForReward(quest);
            quest.OnCaravanDestroyed(winners.LeaderParty);
            return;
        }
        if (!OnSide(losers, quest._banditParty)) return;
        if (quest._isCaravanSaved) return;
        quest._isCaravanSaved = true;
        HoldCaravanForReward(quest);
        var ownerParty = MobileParty.MainParty;
        // A retreated party stays in the winners list until the event is finalized.
        var ownerWon = ownerParty != null && !retreatedPartyTracker.IsRetreated(battle, ownerParty.Party) &&
            OnSide(winners, ownerParty);
        var caravanOnWinningSide = quest._caravanParty.IsActive && OnSide(winners, quest._caravanParty);
        if (!ownerWon)
        {
            quest.OnCaravanSurvivedWithoutHelp();
        }
        else if (!caravanOnWinningSide)
        {
            quest.OnQuestSucceeded();
        }
        else
        {
            publisher.SendConversation(quest);
        }
    }

    private void HoldCaravanForReward(Quest quest)
    {
        if (quest._isCaravanSaved && quest._caravanParty?.IsActive == true)
        {
            // Keep the caravan available for the owner's reward conversation.
            quest._caravanParty.SetMoveGoToPoint(quest._caravanParty.Position, MobileParty.NavigationType.Default);
            quest._caravanParty.Ai.SetDoNotMakeNewDecisions(true);
            ReleaseParty(quest._banditParty, quest._banditParty?.HomeSettlement);
        }
        else
        {
            Release(quest);
        }
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
