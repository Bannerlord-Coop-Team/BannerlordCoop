using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>The server's unstarted quest listens through the dispatcher, in its owner's context.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersWorldPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTickPostfix() => Run((service, quest) => service.ApplyTick(quest));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnStartBattle))]
    [HarmonyPostfix]
    private static void BattleStartedPostfix(PartyBase attackerParty) =>
        Run((service, quest) => service.ApplyBattleStarted(quest, attackerParty));

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    private static void FinalizeEventPrefix(MapEvent __instance, bool __runOriginal, out Dictionary<Quest, bool?> __state)
    {
        __state = null;
        if (ModInformation.IsClient || !__runOriginal || __instance.IsFinalized) return;

        // Finalization clears the parties and their sides before the outcome is applied.
        var outcomes = new Dictionary<Quest, bool?>();
        Run((service, quest) =>
        {
            if (service.TryCaptureBattleOutcome(quest, __instance, out var ownerWon)) outcomes.Add(quest, ownerWon);
        });
        __state = outcomes;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPostfix]
    private static void FinalizeEventPostfix(MapEvent __instance, bool __runOriginal, Dictionary<Quest, bool?> __state)
    {
        if (!__runOriginal || !__instance.IsFinalized || __state == null || __state.Count == 0) return;
        Run((service, quest) =>
        {
            if (__state.TryGetValue(quest, out var ownerWon)) service.ApplyBattleFinalized(quest, ownerWon);
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        // QuestWorldEventRelayPatches runs vanilla for coercion. This postfix handles attacks on village parties.
        if (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) return;
        Run((service, quest) => service.ApplyMapEventStarted(quest, mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    // The shared coercion relay calls vanilla on the server in the owner's context.
    private static bool MapStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftPostfix(MobileParty party, Settlement settlement) =>
        Run((service, quest) => service.ApplySettlementLeft(quest, party, settlement));

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementLeft))]
    [HarmonyPrefix]
    // ApplySettlementLeft handles the owner leaving the village.
    private static bool LeftPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.StartAmbushEncounter))]
    [HarmonyPrefix]
    // The server starts the ambush and the owner opens the encounter when its notice arrives.
    private static bool StartAmbushEncounterPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.GameMenuOpened))]
    [HarmonyPrefix]
    // FinalizeEventAux cleans up the quest parties without waiting for the encounter menu.
    private static bool GameMenuOpenedPrefix() => false;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) =>
        Run((service, quest) => service.ApplyVillageBeingRaided(quest, village));

    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventEnded))]
    [HarmonyPrefix]
    // FinalizeEventAux applies the outcome after clearing the battle sides.
    private static bool MapEventEndedPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    // ApplyVillageBeingRaided handles the raid when the event reaches the server.
    private static bool RaidPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.TickDesertersPartyLogic))]
    [HarmonyPrefix]
    // ApplyTick replaces the vanilla tick on the server, and clients replay the resulting progress.
    private static bool TickPrefix() => false;

    private static void Run(Action<IExtortionByDesertersWorldEventService, Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) &&
            ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
        {
            dispatcher.RunAsOwners<Quest>(quest => listener(service, quest));
        }
    }
}
