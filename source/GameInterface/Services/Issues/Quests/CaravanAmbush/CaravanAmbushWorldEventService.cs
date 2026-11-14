using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Participation;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushWorldEventService
{
    void HourlyTick();
    void MapEventStarted(MapEvent battle, PartyBase attacker, PartyBase defender);
    void BanditsRecruited(MobileParty bandits);
    void SettlementEntered(MobileParty party);
    void BattleEnded(MapEvent battle);
    void End(Quest quest);
}

internal enum CaravanAmbushBattleOutcome
{
    None,
    CaravanLost,
    SurvivedWithoutHelp,
    DirectSuccess,
    HoldForReward,
}

internal class CaravanAmbushWorldEventService : ICaravanAmbushWorldEventService
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly ICaravanAmbushProgress progress;
    private readonly IRetreatedMapEventPartyTracker retreatedPartyTracker;

    public CaravanAmbushWorldEventService(IOutcomeDispatcher dispatcher, ICaravanAmbushProgress progress,
        IRetreatedMapEventPartyTracker retreatedPartyTracker)
    {
        this.dispatcher = dispatcher;
        this.progress = progress;
        this.retreatedPartyTracker = retreatedPartyTracker;
    }

    public void HourlyTick() => Run(quest =>
    {
        // Vanilla's vicinity check reads MainParty, which is only the owner's when the owner has a party.
        var skipsVicinityCheck = quest._caravanParty?.MapEvent != null || quest._isCaravanSaved;
        if (Hero.MainHero.PartyBelongedTo != null || skipsVicinityCheck) quest.HourlyTick();
        return false;
    });

    public void MapEventStarted(MapEvent battle, PartyBase attacker, PartyBase defender) => Run(quest =>
    {
        quest.MapEventStarted(battle, attacker, defender);
        return false;
    });

    public void BanditsRecruited(MobileParty bandits)
    {
        var recruiter = Hero.MainHero;
        Run(quest =>
        {
            if (quest._banditParty != bandits) return false;
            if (Hero.MainHero == recruiter) quest.OnPlayerHiredBandits();
            else quest.CompleteQuestWithCancel();
            return false;
        });
    }

    public void SettlementEntered(MobileParty party) => Run(quest =>
    {
        if (party == null || quest._caravanParty != party || quest._isCaravanSaved) return false;
        DestroyPartyAction.Apply(party.Party, party);
        quest._caravanParty = null;
        quest.CompleteQuestWithCancel();
        return false;
    });

    public void End(Quest quest)
    {
        quest.RemoveAllTrackedObjects();
        Release(quest);
    }

    public void BattleEnded(MapEvent battle) => Run(quest => BattleEnded(quest, battle));

    private void Run(Func<Quest, bool> action)
    {
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            var before = progress.Capture(quest);
            var openRewardConversation = action(quest);
            if (quest.IsOngoing && progress.Capture(quest) != before)
                progress.Publish(quest, openRewardConversation);
        });
    }

    internal static CaravanAmbushBattleOutcome Classify(bool caravanLost, bool banditsLost, bool alreadySaved,
        bool ownerWon, bool caravanOnWinningSide)
    {
        if (caravanLost) return CaravanAmbushBattleOutcome.CaravanLost;
        if (!banditsLost || alreadySaved) return CaravanAmbushBattleOutcome.None;
        if (!ownerWon) return CaravanAmbushBattleOutcome.SurvivedWithoutHelp;
        if (caravanOnWinningSide) return CaravanAmbushBattleOutcome.HoldForReward;
        return CaravanAmbushBattleOutcome.DirectSuccess;
    }

    private bool BattleEnded(Quest quest, MapEvent battle)
    {
        if (quest._caravanParty == null || quest._banditParty == null ||
            battle.WinningSide == BattleSideEnum.None || battle.DefeatedSide == BattleSideEnum.None) return false;
        var winners = battle.GetMapEventSide(battle.WinningSide);
        var losers = battle.GetMapEventSide(battle.DefeatedSide);
        bool OnSide(MapEventSide side, MobileParty party) => side.Parties.Any(entry => entry.Party == party.Party);
        var ownerParty = Hero.MainHero.PartyBelongedTo;
        // A retreated party stays in the winners list until the event is finalized.
        var ownerWon = ownerParty != null && !retreatedPartyTracker.IsRetreated(battle, ownerParty.Party) &&
            OnSide(winners, ownerParty);
        var outcome = Classify(OnSide(losers, quest._caravanParty), OnSide(losers, quest._banditParty),
            quest._isCaravanSaved, ownerWon, quest._caravanParty.IsActive && OnSide(winners, quest._caravanParty));
        switch (outcome)
        {
            case CaravanAmbushBattleOutcome.CaravanLost:
                Release(quest);
                quest.OnCaravanDestroyed(winners.LeaderParty);
                break;
            case CaravanAmbushBattleOutcome.SurvivedWithoutHelp:
                quest._isCaravanSaved = true;
                quest.OnCaravanSurvivedWithoutHelp();
                break;
            case CaravanAmbushBattleOutcome.DirectSuccess:
                quest._isCaravanSaved = true;
                quest.OnQuestSucceeded();
                break;
            case CaravanAmbushBattleOutcome.HoldForReward:
                quest._isCaravanSaved = true;
                HoldCaravanForReward(quest);
                return true;
        }
        return false;
    }

    private void HoldCaravanForReward(Quest quest)
    {
        quest._caravanParty.SetMoveGoToPoint(quest._caravanParty.Position, MobileParty.NavigationType.Default);
        quest._caravanParty.Ai.SetDoNotMakeNewDecisions(true);
        ReleaseParty(quest._banditParty, quest._banditParty.HomeSettlement);
    }

    private void Release(Quest quest)
    {
        ReleaseParty(quest._caravanParty, quest._targetSettlement);
        ReleaseParty(quest._banditParty, quest._banditParty?.HomeSettlement);
    }

    private static void ReleaseParty(MobileParty party, Settlement destination)
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
