using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatch]
internal class CaravanAmbushEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void Hourly() => Run(quest =>
    {
        if (quest.QuestDueTime.IsPast) quest.CompleteQuestWithTimeOut();
        else quest.HourlyTick();
    });

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void BattleStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty) =>
        Run(quest => quest.MapEventStarted(mapEvent, attackerParty, defenderParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void BattleEnded(MapEvent mapEvent)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events))
            events.Run(quest => events.BattleEnded(quest, mapEvent));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void Arrived(MobileParty party, Settlement settlement, Hero hero) => Run(quest =>
    {
        if (party == null || quest._caravanParty != party || quest._isCaravanSaved) return;
        quest.HandlePartyAiAfterCompletion();
        DestroyPartyAction.Apply(party.Party, party);
        quest._caravanParty = null;
        quest.CompleteQuestWithCancel();
    });

    // The dedicated server has no player clan; the accept preconditions check war as the requesting player.
    [HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
    [HarmonyPrefix]
    private static bool StayAlive(Issue __instance, ref bool __result)
    {
        __result = __instance.IssueOwner != null && __instance.IssueOwner.OwnedCaravans.Count > 0;
        return false;
    }

    private static void Run(Action<Quest> action)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events)) events.Run(action);
    }
}
