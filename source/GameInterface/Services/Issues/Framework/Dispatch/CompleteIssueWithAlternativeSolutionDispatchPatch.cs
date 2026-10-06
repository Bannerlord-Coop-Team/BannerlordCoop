using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

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
    private static void Finalizer(IDisposable __state)
    {
        __state?.Dispose();
    }
}
