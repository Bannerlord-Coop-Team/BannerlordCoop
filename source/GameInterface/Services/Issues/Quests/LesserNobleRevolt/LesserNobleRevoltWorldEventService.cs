using Common.Network;
using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal interface ILesserNobleRevoltWorldEventService
{
    void SendOwnerBattleEnded(Quest quest);
    void HourlyTickParty(MobileParty party);
    void SettlementEntered(MobileParty party, Settlement settlement, Hero hero);
    void MapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender);
    void MapEventEnded(MapEvent mapEvent);
    bool IsProtectedHero(Hero hero);
    bool IsOwnRevoltParty(PartyBase capturerParty, Hero prisoner);
    void ForgetOwnerBattle(Quest quest);
}

internal sealed class LesserNobleRevoltWorldEventService : ILesserNobleRevoltWorldEventService
{
    private readonly Dictionary<Quest, (Hero Owner, MapEvent Battle)> activeOwnerBattles =
        new Dictionary<Quest, (Hero Owner, MapEvent Battle)>();
    private readonly IOutcomeDispatcher dispatcher;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public LesserNobleRevoltWorldEventService(
        IIssueOwnerResolver ownerResolver,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IOutcomeDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        this.ownerResolver = ownerResolver;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
    }

    public void SendOwnerBattleEnded(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !playerManager.TryGetPeer(controllerId, out var peer) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        network.Send(peer, new NetworkLesserNobleRevoltOwnerBattleEnded(issueOwnerId, issue.StringId, controllerId));
    }

    public void HourlyTickParty(MobileParty mobileParty)
    {
        if (!mobileParty.IsCurrentlyUsedByAQuest)
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            var notificationPending = quest._giveNotificationAfterHoursCounter > 0;
            quest.HourlyTickParty(mobileParty);
            if (notificationPending && quest._giveNotificationAfterHoursCounter == -1 &&
                quest._visitedVillagesList.Count > 0)
            {
                SendJournalUpdate(quest, quest._visitedVillagesList.Last(), GetVisitProgress(quest), true);
            }
        });
    }

    public void SettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        // Vanilla only reacts to its own revolt party, which is marked as used by a quest.
        if (party?.IsCurrentlyUsedByAQuest != true)
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            if (IsRevoltPartyVisit(quest, party))
            {
                // Sent before vanilla runs, because the twentieth visit times the quest out.
                SendJournalUpdate(quest, settlement, GetVisitProgress(quest) + 1, false);
            }

            quest.OnSettlementEntered(party, settlement, hero);
        });
    }

    public void MapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            if (quest.IsOngoing && quest._lesserNobleParty != null &&
                attackerParty == MobileParty.MainParty?.Party &&
                defenderParty == quest._lesserNobleParty.Party)
            {
                RecordOwnerBattle(quest, Hero.MainHero, mapEvent);
            }

            quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty);
        });
    }

    public void MapEventEnded(MapEvent mapEvent)
    {
        dispatcher.RunAsOwners<Quest>(quest => CompleteBattleFromMapEvent(quest, mapEvent));
    }

    internal void CompleteBattleFromMapEvent(Quest quest, MapEvent mapEvent)
    {
        var revoltParty = quest._lesserNobleParty?.Party;
        if (!quest.IsOngoing || revoltParty == null)
        {
            return;
        }

        // A retreating owner leaves InvolvedParties, so the battle it started is remembered separately.
        var ownerStarted = activeOwnerBattles.TryGetValue(quest, out var ownerBattle) && ownerBattle.Battle == mapEvent;
        if (!mapEvent.InvolvedParties.Contains(revoltParty))
        {
            if (ownerStarted)
            {
                // The revolt party left the owner's battle, so it ends without a result.
                EndOwnerBattle(quest);
            }

            return;
        }

        var ownerParty = MobileParty.MainParty?.Party;
        var ownerInvolved = ownerParty != null && mapEvent.InvolvedParties.Contains(ownerParty);
        var hasWinner = mapEvent.WinningSide != BattleSideEnum.None;
        var revoltWon = hasWinner &&
            mapEvent.GetMapEventSide(mapEvent.WinningSide).Parties.Any(entry => entry.Party == revoltParty);

        ApplyBattleEnd(quest, ClassifyBattleEnd(ownerStarted, ownerInvolved, hasWinner, revoltWon));
    }

    internal void ApplyBattleEnd(Quest quest, RevoltBattleEnd end)
    {
        if (end == RevoltBattleEnd.Ignored)
        {
            return;
        }

        // Released before the branch runs, so a throwing branch cannot leave the owner protected.
        EndOwnerBattle(quest);
        switch (end)
        {
            case RevoltBattleEnd.NoWinner:
                break;
            case RevoltBattleEnd.OwnerWon:
                quest.QuestSuccessWithPlayerDefeatedLesserNobleParty();
                break;
            case RevoltBattleEnd.OwnerLost:
                quest.QuestFailWithPlayerDefeatedAgainstLesserNobleParty();
                break;
            case RevoltBattleEnd.ThirdPartyWon:
                quest.RelationshipChangeWithQuestGiver = 0;
                quest.CompleteQuestWithCancel(new TextObject("{=str_coop_lesser_noble_revolt_canceled}The revolt was defeated by another party. Your agreement has been canceled."));
                break;
        }
    }

    private void EndOwnerBattle(Quest quest)
    {
        quest._firstTalkIsDone = true;
        quest._checkForEventEnd = false;

        // Vanilla's next talk reads the owner client's copy, which never sees the server's battle end.
        if (activeOwnerBattles.Remove(quest))
        {
            SendOwnerBattleEnded(quest);
        }
    }

    internal enum RevoltBattleEnd
    {
        Ignored,
        NoWinner,
        OwnerWon,
        OwnerLost,
        ThirdPartyWon,
    }

    internal static RevoltBattleEnd ClassifyBattleEnd(bool ownerStarted, bool ownerInvolved, bool hasWinner, bool revoltWon)
    {
        if (!hasWinner)
        {
            return ownerStarted ? RevoltBattleEnd.NoWinner : RevoltBattleEnd.Ignored;
        }

        if (ownerStarted || ownerInvolved)
        {
            return revoltWon ? RevoltBattleEnd.OwnerLost : RevoltBattleEnd.OwnerWon;
        }

        // A revolt victory over someone else leaves the owner's quest and dialogue untouched.
        return revoltWon ? RevoltBattleEnd.Ignored : RevoltBattleEnd.ThirdPartyWon;
    }

    internal static bool IsRevoltPartyVisit(Quest quest, MobileParty party)
    {
        return party != null && party == quest._lesserNobleParty && party != MobileParty.MainParty;
    }

    public void ForgetOwnerBattle(Quest quest)
    {
        activeOwnerBattles.Remove(quest);
    }

    internal void RecordOwnerBattle(Quest quest, Hero owner, MapEvent battle) =>
        activeOwnerBattles[quest] = (owner, battle);

    /// <summary>
    /// Applies the server's <paramref name="visitCount"/>-th revolt visit. A visit the client already
    /// holds is ignored, so a repeated packet cannot advance the support log twice.
    /// </summary>
    internal static bool ApplyClientSettlementProgress(Quest quest, Settlement settlement, int visitCount)
    {
        if (settlement == null || GetVisitProgress(quest) >= visitCount)
        {
            return false;
        }

        quest._visitedVillagesList.Add(settlement);
        quest._suitableVillagesToVisitList.Remove(settlement);
        quest._goOutFromSettlementAfterHoursCounter = 5;
        quest._giveNotificationAfterHoursCounter = 9;
        quest._discreteLog?.UpdateCurrentProgress(quest._discreteLog.CurrentProgress + 1);
        return true;
    }

    /// <summary>
    /// Vanilla gives one location notification per visit and marks it given with -1, so a second
    /// notification for the same visit is ignored.
    /// </summary>
    internal static bool ShouldApplyClientNotification(Quest quest, int visitCount)
    {
        return quest._giveNotificationAfterHoursCounter != -1 &&
            GetVisitProgress(quest) > 0 &&
            GetVisitProgress(quest) == visitCount;
    }

    private static int GetVisitProgress(Quest quest) =>
        quest._discreteLog?.CurrentProgress ?? quest._visitedVillagesList.Count;

    internal static void ApplyClientHourlyJournalNotification(Quest quest, Settlement settlement)
    {
        var locationLog = quest.QuestLesserNoblePartyLocationLog;
        locationLog.SetTextVariable("VILLAGE", settlement.EncyclopediaLinkWithName);
        MBInformationManager.AddQuickInformation(locationLog);
        quest.AddLog(locationLog);
        foreach (var visited in quest._visitedVillagesList)
        {
            if (quest.IsTracked(visited))
            {
                quest.RemoveTrackedObject(visited);
            }
        }

        quest.AddTrackedObject(settlement);
        quest._giveNotificationAfterHoursCounter = -1;
    }

    public bool IsProtectedHero(Hero hero)
    {
        return hero != null && activeOwnerBattles.Values.Any(battle => battle.Owner == hero);
    }

    internal void SendJournalUpdate(Quest quest, Settlement settlement, int visitCount, bool notification)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (ModInformation.IsClient || issue == null || settlement == null ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !playerManager.TryGetPeer(controllerId, out var peer) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId) ||
            !objectManager.TryGetIdWithLogging(settlement, out var settlementId))
        {
            return;
        }

        network.Send(peer, new NetworkLesserNobleRevoltJournalUpdate(
            issueOwnerId,
            issue.StringId,
            controllerId,
            settlementId,
            visitCount,
            notification));
    }

    // Vanilla never lets the revolt party hold its owner, and destroying it later inside the owner's
    // scope would strand them, so the owner is not captured even in a battle another player started.
    public bool IsOwnRevoltParty(PartyBase capturerParty, Hero prisoner)
    {
        if (capturerParty == null || prisoner == null || Campaign.Current?.IssueManager == null)
        {
            return false;
        }

        return Campaign.Current.IssueManager.Issues.Values.Any(issue =>
            issue.IssueQuest is Quest quest && quest.IsOngoing &&
            quest._lesserNobleParty?.Party == capturerParty &&
            ownerResolver.TryResolveOwnerHero(issue, out var owner) && owner == prisoner);
    }
}
