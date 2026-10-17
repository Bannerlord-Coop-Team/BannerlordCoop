using GameInterface.Services.Issues.Framework.Interface;
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
    void BattleEnded(Quest quest, MapEvent battle);
    void Release(Quest quest);
}

internal class CaravanAmbushEvents : ICaravanAmbushEvents
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly ICaravanAmbushStatePublisher publisher;

    public CaravanAmbushEvents(IOutcomeDispatcher dispatcher, ICaravanAmbushStatePublisher publisher)
    {
        this.dispatcher = dispatcher;
        this.publisher = publisher;
    }

    public void Run(Action<Quest> action)
    {
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            action(quest);
            if (quest.IsOngoing) publisher.Publish(quest);
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
            Release(quest);
            quest.OnCaravanDestroyed(winners.LeaderParty);
            return;
        }
        if (!losers.Parties.Any(party => party.Party == quest._banditParty.Party)) return;
        quest._isCaravanSaved = true;
        Release(quest);
        if (!winners.Parties.Any(party => party.Party == MobileParty.MainParty.Party))
        {
            quest.OnCaravanSurvivedWithoutHelp();
        }
        else if (!quest._caravanParty.IsActive || !winners.Parties.Any(party => party.Party == quest._caravanParty.Party))
        {
            quest.OnQuestSucceeded();
        }
        // The owner talks to the surviving caravan on their client to collect its goods.
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
