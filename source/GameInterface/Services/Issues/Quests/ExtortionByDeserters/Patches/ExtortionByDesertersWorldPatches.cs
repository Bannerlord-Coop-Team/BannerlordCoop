using Common;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
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
    private static void HourlyTickPostfix() => Run(quest => quest.HourlyTick());

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnStartBattle))]
    [HarmonyPostfix]
    private static void BattleStartedPostfix(PartyBase attackerParty) =>
        Run(quest =>
        {
            if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
                service.TryStartAmbush(quest, attackerParty);
        });

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    private static void FinalizeEventPrefix(MapEvent __instance, out List<Quest> __state)
    {
        var affected = new List<Quest>();
        Run(quest => { if (quest._deserterMobileParty?.MapEvent == __instance) affected.Add(quest); });
        __state = affected;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPostfix]
    private static void FinalizeEventPostfix(List<Quest> __state)
    {
        if (__state == null || __state.Count == 0) return;
        Run(quest =>
        {
            if (!__state.Contains(quest)) return;
            if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
            {
                service.ApplyBattleFinalized(quest);
            }
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) return;
        Run(quest =>
        {
            if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
                service.ApplyMapEventStarted(quest, mapEvent, attackerParty, defenderParty);
        });
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapStartedPrefix() => false;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftPostfix(MobileParty party, Settlement settlement) =>
        Run(quest =>
        {
            if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
                service.ApplySettlementLeft(quest, party, settlement);
        });

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool LeftPrefix() => false;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) =>
        Run(quest =>
        {
            if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
                service.ApplyVillageBeingRaided(quest, village);
        });

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEndedPostfix(MapEvent mapEvent)
    {
        Run(quest =>
        {
            if (quest._deserterMobileParty?.MapEvent == mapEvent &&
                ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
                service.ApplyMapEventEnded(quest, mapEvent);
        });
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventEnded))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    private static bool RaidPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.TickDesertersPartyLogic))]
    [HarmonyPrefix]
    private static bool TickPrefix(Quest __instance)
    {
        // The server runs the quest tick for the owner, while clients only replay progress.
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
        {
            service.ApplyTick(__instance);
        }
        return false;
    }

    private static void Run(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(listener);
    }

}
