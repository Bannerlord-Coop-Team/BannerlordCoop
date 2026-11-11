using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class LadysKnightOutEventPatches
{
    [ThreadStatic]
    private static Hero warInitiator;
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) return;
        Run(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPrefix]
    private static void CaptureWarInitiator(out Hero __state)
    {
        __state = warInitiator;
        warInitiator = ResolvedMainHeroContext.ResolvedMainHero;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyFinalizer]
    private static void RestoreWarInitiator(Hero __state) => warInitiator = __state;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static void WarDeclaredPrefix(ref DeclareWarAction.DeclareWarDetail detail)
    {
        if (ModInformation.IsClient) return;
        var initiator = warInitiator ?? ResolvedMainHeroContext.ResolvedMainHero;
        if (detail == DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility && initiator != Hero.MainHero)
            detail = DeclareWarAction.DeclareWarDetail.Default;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void DeparturePostfix(MobileParty party, Settlement settlement) =>
        Run(quest => quest.OnSettlementLeft(party, settlement));

    private static void Run(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(listener);
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class LadysKnightOutWorldListenerPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { nameof(Quest.OnMapEventStarted), nameof(Quest.OnHeroKilled),
            nameof(Quest.OnSettlementLeft), nameof(Quest.OnTimedOut) })
            yield return AccessTools.DeclaredMethod(typeof(Quest), name);
    }

    private static bool Prefix() => ModInformation.IsServer;
}
