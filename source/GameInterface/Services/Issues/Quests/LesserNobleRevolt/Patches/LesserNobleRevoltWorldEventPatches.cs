using Common;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltWorldEventPatches
{
    private static readonly Dictionary<Quest, (Hero Owner, MapEvent Battle)> ActiveOwnerBattles =
        new Dictionary<Quest, (Hero Owner, MapEvent Battle)>();

    // The owner client's own record of the revolt battle it started, for its dialogue state only.
    private static readonly Dictionary<Quest, MapEvent> ClientOwnerBattles = new Dictionary<Quest, MapEvent>();

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTickParty))]
    [HarmonyPostfix]
    private static void HourlyTickParty(MobileParty mobileParty)
    {
        if (!mobileParty.IsCurrentlyUsedByAQuest)
        {
            return;
        }

        RunAsOwners(quest =>
        {
            var notificationPending = quest._giveNotificationAfterHoursCounter > 0;
            quest.HourlyTickParty(mobileParty);
            if (notificationPending && quest._giveNotificationAfterHoursCounter == -1 &&
                quest._visitedVillagesList.Count > 0)
            {
                SendJournalUpdate(quest, quest._visitedVillagesList.Last(), quest._visitedVillagesList.Count, true);
            }
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTick()
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        RunAsOwners(quest =>
        {
            if (quest.IsOngoing && quest.QuestDueTime.IsPast)
            {
                quest.OnTimedOut();
            }
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void SettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        // Vanilla only reacts to its own revolt party, which is marked as used by a quest.
        if (party?.IsCurrentlyUsedByAQuest != true)
        {
            return;
        }

        RunAsOwners(quest =>
        {
            if (IsRevoltPartyVisit(quest, party))
            {
                // Sent before vanilla runs, because the twentieth visit times the quest out.
                SendJournalUpdate(quest, settlement, quest._visitedVillagesList.Count + 1, false);
            }

            quest.OnSettlementEntered(party, settlement, hero);
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        RunAsOwners(quest =>
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

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEventEnded(MapEvent mapEvent)
    {
        RunAsOwners(quest => CompleteBattleFromMapEvent(quest, mapEvent));
    }

    // The owner's journal follows the server's NetworkLesserNobleRevoltJournalUpdate only, so a
    // local tick or settlement event cannot apply the same visit or notification a second time.
    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTickParty))]
    [HarmonyPrefix]
    private static bool HourlyTickPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementEntered))]
    [HarmonyPrefix]
    private static bool SettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix(Quest __instance, MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        // Like vanilla, only ever set: another battle starting must not clear a pending revolt battle.
        var playerParty = MobileParty.MainParty?.Party;
        if (__instance.IsOngoing &&
            __instance._lesserNobleParty != null &&
            playerParty != null &&
            attackerParty == playerParty &&
            defenderParty == __instance._lesserNobleParty.Party)
        {
            __instance._checkForEventEnd = true;
            ClientOwnerBattles[__instance] = mapEvent;
        }

        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnd))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix(Quest __instance, MapEvent obj)
    {
        if (ModInformation.IsClient && __instance._checkForEventEnd &&
            IsClientOwnerBattleEnd(__instance, obj))
        {
            __instance._firstTalkIsDone = true;
            __instance._checkForEventEnd = false;
            ClientOwnerBattles.Remove(__instance);
        }

        return false;
    }

    // Matches the server's EndOwnerBattle, which also resets when the revolt party left the battle.
    internal static bool IsClientOwnerBattleEnd(Quest quest, MapEvent mapEvent)
    {
        return (ClientOwnerBattles.TryGetValue(quest, out var battle) && battle == mapEvent) ||
            (quest._lesserNobleParty != null && mapEvent.InvolvedParties.Contains(quest._lesserNobleParty.Party));
    }

    // The server strips militia from the owner's battle in LesserNobleRevoltBattlePatches.
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnGameMenuOpened))]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix() => ModInformation.IsServer;

    // The server fails the quest when it times out, a client replaying the timeout only ends its copy
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimedOutPrefix() => ModInformation.IsServer;

    private static void CompleteBattleFromMapEvent(Quest quest, MapEvent mapEvent)
    {
        var revoltParty = quest._lesserNobleParty?.Party;
        if (!quest.IsOngoing || revoltParty == null)
        {
            return;
        }

        // A retreating owner leaves InvolvedParties, so the battle it started is remembered separately.
        var ownerStarted = ActiveOwnerBattles.TryGetValue(quest, out var ownerBattle) && ownerBattle.Battle == mapEvent;
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

    internal static void ApplyBattleEnd(Quest quest, RevoltBattleEnd end)
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
                // A third party can defeat the revolt party while its owner's quest is running.
                quest.QuestSuccessWithPlayerDefeatedLesserNobleParty();
                break;
        }
    }

    private static void EndOwnerBattle(Quest quest)
    {
        quest._firstTalkIsDone = true;
        quest._checkForEventEnd = false;
        ActiveOwnerBattles.Remove(quest);
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

    internal static void ForgetOwnerBattle(Quest quest)
    {
        ActiveOwnerBattles.Remove(quest);
        ClientOwnerBattles.Remove(quest);
    }

    internal static void RecordOwnerBattle(Quest quest, Hero owner, MapEvent battle) =>
        ActiveOwnerBattles[quest] = (owner, battle);

    /// <summary>
    /// Applies the server's <paramref name="visitCount"/>-th revolt visit. A visit the client already
    /// holds is ignored, so a repeated packet cannot advance the support log twice.
    /// </summary>
    internal static bool ApplyClientSettlementProgress(Quest quest, Settlement settlement, int visitCount)
    {
        if (settlement == null || quest._visitedVillagesList.Count >= visitCount)
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
            quest._visitedVillagesList.Count > 0 &&
            quest._visitedVillagesList.Count == visitCount;
    }

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

    internal static bool IsProtectedHero(Hero hero)
    {
        return hero != null && ActiveOwnerBattles.Values.Any(battle => battle.Owner == hero);
    }

    private static void SendJournalUpdate(Quest quest, Settlement settlement, int visitCount, bool notification)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (ModInformation.IsClient || issue == null || settlement == null ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ownerResolver.TryGetOwnerControllerId(issue, out var controllerId) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId) ||
            !objectManager.TryGetIdWithLogging(settlement, out var settlementId))
        {
            return;
        }

        network.SendAll(new NetworkLesserNobleRevoltJournalUpdate(
            issueOwnerId,
            issue.StringId,
            controllerId,
            settlementId,
            visitCount,
            notification));
    }

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners(listener);
    }
}
