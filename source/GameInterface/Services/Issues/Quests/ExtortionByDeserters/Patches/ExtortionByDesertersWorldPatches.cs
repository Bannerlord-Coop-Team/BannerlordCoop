using Common;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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
    private static void BattleStartedPostfix(PartyBase attackerParty)
    {
        Run(quest =>
        {
            if (attackerParty == quest._deserterMobileParty?.Party &&
                ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
                service.TryStartAmbush(quest);
        });
    }

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
                service.TryCleanupBattle(quest);
            }
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) return;
        Run(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapStartedPrefix(Quest __instance, MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        // Server decisions use the injected quest service instead of a static Harmony body.
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
        {
            service.ApplyMapEventStarted(__instance, mapEvent, attackerParty, defenderParty);
        }
        return false;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftPostfix(MobileParty party, Settlement settlement) =>
        Run(quest => quest.OnSettlementLeft(party, settlement));

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool LeftPrefix(Quest __instance, MobileParty party, Settlement settlement)
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
        {
            service.ApplySettlementLeft(__instance, party, settlement);
        }
        return false;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) => Run(quest => quest.OnVillageBeingRaided(village));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEndedPostfix(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        Run(quest =>
        {
            if (quest._deserterMobileParty?.MapEvent == mapEvent)
                quest.MapEventEnded(mapEvent);
        });
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventEnded))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix(Quest __instance, MapEvent mapEvent)
    {
        // The owner party comes from the issue resolver, unlike vanilla's MainParty.
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
        {
            service.ApplyMapEventEnded(__instance, mapEvent);
        }
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    private static bool RaidPrefix(Quest __instance, Village village)
    {
        if (ContainerProvider.TryResolve<IExtortionByDesertersWorldEventService>(out var service))
        {
            service.ApplyVillageBeingRaided(__instance, village);
        }
        return false;
    }

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
