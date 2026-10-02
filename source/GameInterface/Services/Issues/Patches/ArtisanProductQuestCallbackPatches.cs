using Common;
using GameInterface.Services.Issues.Interfaces;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[HarmonyPatch]
internal sealed class ArtisanProductQuestCallbackPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), "OnWarDeclared");
        yield return AccessTools.Method(typeof(Quest), "OnClanChangedKingdom");
        yield return AccessTools.Method(typeof(Quest), "OnHeroKilled");
    }

    [HarmonyPrefix]
    private static bool EnterOwner(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (ModInformation.IsClient) return false;
        return ContainerProvider.TryResolve<IArtisanProductAuthority>(out var authority) &&
            authority.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void ExitOwner(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal sealed class ArtisanProductIssueCallbackPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithQuest));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithTimedOut));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithStayAliveConditionsFailed));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithBetrayal));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithFail));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord));
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool EnterOwner(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue) return true;
        if (__instance.IssueOwner?.Issue != __instance) return false;
        if (ModInformation.IsClient) return AllowedThread.IsThisThreadAllowed();
        if (!__instance.IsTriedToSolveBefore)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }
        return ContainerProvider.TryResolve<IArtisanProductAuthority>(out var authority) &&
            authority.TryEnter(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void ExitOwner(IDisposable __state) => __state?.Dispose();
}
