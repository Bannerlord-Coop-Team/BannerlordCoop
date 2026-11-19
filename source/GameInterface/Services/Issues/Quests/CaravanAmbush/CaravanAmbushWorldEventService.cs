using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Participation;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
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
    void BanditsRecruited(MobileParty bandits, Hero recruiter);
    void SettlementEntered(MobileParty party);
    void BattleEnded(MapEvent battle);
    void End(Quest quest);
}

internal class CaravanAmbushWorldEventService : ICaravanAmbushWorldEventService
{
    // From CaravanAmbushIssueQuest.HourlyTick.
    private const float AmbushTriggerRadiusFactor = 2.2f;

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
        // Vanilla only reads MainParty for vicinity and escort checks while the caravan is unsaved and out of battle.
        // A caravan already in battle or saved can run the vanilla tick even if the owner has no party.
        var ownerRunsVanillaTick = Hero.MainHero.PartyBelongedTo != null ||
            quest._caravanParty?.MapEvent != null || quest._isCaravanSaved;
        if (ownerRunsVanillaTick)
        {
            quest.HourlyTick();
            return;
        }

        // Without an owner party, run only the ambush trigger.
        var banditsReachedCaravan = quest._caravanParty != null && quest._banditParty != null &&
            quest._caravanParty.Position.DistanceSquared(quest._banditParty.Position) <=
            Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * AmbushTriggerRadiusFactor;
        if (banditsReachedCaravan)
        {
            EncounterManager.StartPartyEncounter(quest._banditParty.Party, quest._caravanParty.Party);
        }
    });

    public void MapEventStarted(MapEvent battle, PartyBase attacker, PartyBase defender) => Run(quest =>
    {
        quest.MapEventStarted(battle, attacker, defender);
    });

    public void BanditsRecruited(MobileParty bandits, Hero recruiter)
    {
        Run(quest =>
        {
            if (quest._banditParty != bandits) return;
            if (Hero.MainHero == recruiter) quest.OnPlayerHiredBandits();
            else quest.CompleteQuestWithCancel();
        });
    }

    public void SettlementEntered(MobileParty party) => Run(quest =>
    {
        if (party == null || quest._caravanParty != party || quest._isCaravanSaved) return;
        DestroyPartyAction.Apply(party.Party, party);
        quest._caravanParty = null;
        quest.CompleteQuestWithCancel();
    });

    public void End(Quest quest)
    {
        quest.RemoveAllTrackedObjects();
        Release(quest);
    }

    public void BattleEnded(MapEvent battle) => RunAndPublish(quest =>
        ApplyBattleOutcome(quest, battle) == CaravanAmbushBattleOutcome.HoldForReward);

    private void Run(Action<Quest> action) => RunAndPublish(quest =>
    {
        action(quest);
        return false;
    });

    private void RunAndPublish(Func<Quest, bool> opensRewardConversation)
    {
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            var before = progress.Capture(quest);
            var opensConversation = opensRewardConversation(quest);
            if (quest.IsOngoing && progress.Capture(quest) != before)
                progress.Publish(quest, opensConversation);
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

    private CaravanAmbushBattleOutcome ApplyBattleOutcome(Quest quest, MapEvent battle)
    {
        if (quest._caravanParty == null || quest._banditParty == null ||
            battle.WinningSide == BattleSideEnum.None || battle.DefeatedSide == BattleSideEnum.None)
            return CaravanAmbushBattleOutcome.None;
        var winners = battle.GetMapEventSide(battle.WinningSide);
        var losers = battle.GetMapEventSide(battle.DefeatedSide);
        bool OnSide(MapEventSide side, MobileParty party) => side.Parties.Any(entry => entry.Party == party.Party);
        var ownerParty = Hero.MainHero.PartyBelongedTo;
        // A retreated party stays in the winners list until the event is finalized.
        var ownerWon = ownerParty != null && !retreatedPartyTracker.IsRetreated(battle, ownerParty.Party) &&
            OnSide(winners, ownerParty);
        var outcome = Classify(
            caravanLost: OnSide(losers, quest._caravanParty),
            banditsLost: OnSide(losers, quest._banditParty),
            alreadySaved: quest._isCaravanSaved,
            ownerWon: ownerWon,
            caravanOnWinningSide: quest._caravanParty.IsActive && OnSide(winners, quest._caravanParty));
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
                break;
        }
        return outcome;
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
