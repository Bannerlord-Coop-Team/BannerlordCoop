using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class CompleteIssueWithAlternativeSolutionDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeAlternativeSolutionCompletion(__instance, out __state);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, IDisposable __state)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.AfterAlternativeSolutionCompletion(__instance, __state);
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(IssueBase __instance, IDisposable __state, Exception __exception)
    {
        // A throw skips the postfix, so the issue would stay attached and pay again on the next tick
        if (__exception != null && __state != null && ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return dispatcher.AfterAlternativeSolutionException(__instance, __state, __exception);
        }

        __state?.Dispose();
        return __exception;
    }
}
