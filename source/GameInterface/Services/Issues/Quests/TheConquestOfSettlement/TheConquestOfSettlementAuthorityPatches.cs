using Common;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal static class TheConquestOfSettlementEventPatches
{
    [ThreadStatic] internal static bool IsApplying;
    [ThreadStatic] internal static Hero TriggeringHero;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[]
        {
            nameof(Quest.OnWarDeclared),
            nameof(Quest.OnClanChangedKingdom),
            nameof(Quest.OnMapEventStarted),
            nameof(Quest.OnSiegeCompleted),
            nameof(Quest.OnSettlementOwnerChanged),
            nameof(Quest.OnPeaceDeclared)
        })
        {
            var method = AccessTools.DeclaredMethod(typeof(Quest), name);
            if (method != null) yield return method;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, out (IDisposable Scope, bool WasApplying, Hero PreviousTrigger) __state)
    {
        __state = (null, IsApplying, TriggeringHero);
        // Not IsOriginalAllowed: clients replay the server's wars and ownership changes under AllowedThread,
        // and the quest must then wait for the server's outcome instead of deciding one locally
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        var issue = IssueManager.GetIssueOfQuest(__instance);
        var triggeringHero = ResolvedMainHeroContext.ResolvedMainHero;
        if (ModInformation.IsClient || !__instance.IsOngoing ||
            !ContainerProvider.TryResolve<ITheConquestOfSettlementService>(out var service) ||
            !service.TryOpenOwnerScope(issue, out var scope)) return false;

        IsApplying = true;
        TriggeringHero = triggeringHero;
        __state = (scope, __state.WasApplying, __state.PreviousTrigger);
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((IDisposable Scope, bool WasApplying, Hero PreviousTrigger) __state)
    {
        IsApplying = __state.WasApplying;
        TriggeringHero = __state.PreviousTrigger;
        __state.Scope?.Dispose();
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(DiplomacyHelper), nameof(DiplomacyHelper.IsWarCausedByPlayer))]
internal static class TheConquestOfSettlementWarInitiatorPatch
{
    [HarmonyPostfix]
    private static void Postfix(DeclareWarAction.DeclareWarDetail declareWarDetail, ref bool __result)
    {
        if (ModInformation.IsClient || !TheConquestOfSettlementEventPatches.IsApplying ||
            declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility) return;

        __result = TheConquestOfSettlementEventPatches.TriggeringHero != null &&
            TheConquestOfSettlementEventPatches.TriggeringHero == Hero.MainHero;
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal static class TheConquestOfSettlementRewardPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase issue, ref Hero issueSolver)
    {
        if (issue is Issue && ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate())
        {
            issueSolver = null;
        }
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
internal static class TheConquestOfSettlementTimeoutPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

// The server times the quest out from its own tick, so the relation penalty needs the owner as the main hero
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut))]
internal static class TheConquestOfSettlementTimeoutOwnerPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is Quest && ModInformation.IsServer && __instance.IsOngoing &&
            ContainerProvider.TryResolve<ITheConquestOfSettlementService>(out var service))
        {
            service.TryOpenOwnerScope(IssueManager.GetIssueOfQuest(__instance), out __state);
        }
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
