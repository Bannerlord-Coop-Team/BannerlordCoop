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

[HarmonyPatch]
internal class LadysKnightOutEventPatches
{
    [ThreadStatic]
    private static Hero warInitiator;
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty) =>
        Run(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPrefix]
    private static void CaptureWarInitiator() => warInitiator = ResolvedMainHeroContext.ResolvedMainHero;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void ClearWarInitiator() => warInitiator = null;

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

[HarmonyPatch]
internal class LadysKnightOutNativeTournamentPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnPlayerStartedTournamentMatch));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnPlayerEliminatedFromTournament));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnTournamentFinished));
    }

    // Co-op session results identify each competitor; vanilla callbacks refer to one local player.
    private static bool Prefix() => false;
}
