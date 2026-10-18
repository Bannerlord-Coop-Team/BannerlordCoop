using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
internal static class LesserNobleRevoltWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTickParty))]
    [HarmonyPostfix]
    private static void HourlyTickParty(MobileParty mobileParty)
    {
        if (!mobileParty.IsCurrentlyUsedByAQuest)
        {
            return;
        }

        RunAsOwners(quest => quest.HourlyTickParty(mobileParty));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void SettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        RunAsOwners(quest => quest.OnSettlementEntered(party, settlement, hero));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        RunAsOwners(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEventEnded(MapEvent mapEvent)
    {
        RunAsOwners(quest => CompleteBattleFromMapEvent(quest, mapEvent));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTickParty))]
    [HarmonyPrefix]
    private static bool HourlyTickPartyPrefix(Quest __instance, MobileParty mobileParty)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        ApplyClientHourlyJournal(__instance, mobileParty);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementEntered))]
    [HarmonyPrefix]
    private static bool SettlementEnteredPrefix(Quest __instance, MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        ApplyClientSettlementProgress(__instance, party, settlement);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnd))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool WarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool ClanChangedKingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimedOutPrefix() => ModInformation.IsServer;

    private static void CompleteBattleFromMapEvent(Quest quest, MapEvent mapEvent)
    {
        if (!quest.IsOngoing || !quest._checkForEventEnd || quest._lesserNobleParty == null ||
            !mapEvent.InvolvedParties.Contains(quest._lesserNobleParty.Party) ||
            mapEvent.WinningSide == BattleSideEnum.None)
        {
            return;
        }

        var playerParty = MobileParty.MainParty?.Party;
        if (playerParty == null || !mapEvent.InvolvedParties.Contains(playerParty))
        {
            return;
        }

        var winningSide = mapEvent.GetMapEventSide(mapEvent.WinningSide);
        if (winningSide.Parties.Any(entry => entry.Party == playerParty))
        {
            quest.QuestSuccessWithPlayerDefeatedLesserNobleParty();
        }
        else
        {
            quest.QuestFailWithPlayerDefeatedAgainstLesserNobleParty();
        }

        quest._firstTalkIsDone = true;
        quest._checkForEventEnd = false;
    }

    private static void ApplyClientSettlementProgress(Quest quest, MobileParty party, Settlement settlement)
    {
        if (party != quest._lesserNobleParty)
        {
            return;
        }

        quest._visitedVillagesList.Add(settlement);
        quest._suitableVillagesToVisitList.Remove(settlement);
        quest._goOutFromSettlementAfterHoursCounter = 5;
        quest._giveNotificationAfterHoursCounter = 9;
        quest._discreteLog?.UpdateCurrentProgress(quest._discreteLog.CurrentProgress + 1);
    }

    private static void ApplyClientHourlyJournal(Quest quest, MobileParty mobileParty)
    {
        if (mobileParty != quest._lesserNobleParty)
        {
            return;
        }

        if (quest._giveNotificationAfterHoursCounter > 0)
        {
            quest._giveNotificationAfterHoursCounter--;
        }

        if (quest._giveNotificationAfterHoursCounter != 0 || quest._visitedVillagesList.Count == 0)
        {
            return;
        }

        var settlement = quest._visitedVillagesList[quest._visitedVillagesList.Count - 1];
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

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners(listener);
    }
}
