using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal static class LesserNobleRevoltWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTickParty))]
    [HarmonyPostfix]
    private static void HourlyTickParty(MobileParty mobileParty) => Run(service => service.HourlyTickParty(mobileParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) return;
        Run(service => service.MapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    internal static void SettlementEntered(MobileParty party, Settlement settlement, Hero hero) =>
        Run(service => service.SettlementEntered(party, settlement, hero));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEventEnded(MapEvent mapEvent) => Run(service => service.MapEventEnded(mapEvent));

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
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static void ClearKingdomCancelRelation(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            __instance.RelationshipChangeWithQuestGiver = 0;
        }
    }

    // The server ends the battle in MapEventEnded and announces it to the owner's client.
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnd))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix() => false;

    // The server fails the quest when it times out, a client replaying the timeout only ends its copy
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimedOutPrefix() => ModInformation.IsServer;

    private static void Run(Action<ILesserNobleRevoltWorldEventService> action)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ILesserNobleRevoltWorldEventService>(out var service))
        {
            action(service);
        }
    }
}
