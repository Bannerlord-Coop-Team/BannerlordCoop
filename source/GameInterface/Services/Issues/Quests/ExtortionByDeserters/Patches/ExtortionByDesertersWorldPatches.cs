using Common;
using HarmonyLib;
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
    private static void HourlyTickPostfix()
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service)) service.HourlyTick();
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnStartBattle))]
    [HarmonyPostfix]
    private static void BattleStartedPostfix(PartyBase attackerParty)
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service)) service.BattleStarted(attackerParty);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    private static void FinalizeEventPrefix(MapEvent __instance, bool __runOriginal, out Dictionary<Quest, ExtortionByDesertersBattleOutcome> __state)
    {
        __state = null;
        if (ModInformation.IsClient || !__runOriginal || __instance.IsFinalized) return;

        // Finalization clears the parties and their sides before the outcome is applied.
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
            __state = service.CaptureBattleOutcomes(__instance);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPostfix]
    private static void FinalizeEventPostfix(MapEvent __instance, bool __runOriginal, Dictionary<Quest, ExtortionByDesertersBattleOutcome> __state)
    {
        if (!__runOriginal || !__instance.IsFinalized || __state == null || __state.Count == 0) return;
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service)) service.BattleFinalized(__state);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
            service.MapEventStarted(mapEvent, attackerParty, defenderParty);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftPostfix(MobileParty party, Settlement settlement)
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service)) service.SettlementLeft(party, settlement);
    }

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
    private static void RaidPostfix(Village village)
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service)) service.VillageBeingRaided(village);
    }

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
}
