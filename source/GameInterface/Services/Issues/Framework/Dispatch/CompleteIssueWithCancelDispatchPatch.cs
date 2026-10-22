using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Announces registered issue cancellation and returns troops only on the server.</summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class CompleteIssueWithCancelDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, TextObject log, out (IssueBase Observed, IDisposable Owner) __state)
    {
        __state = default;
        if (ContainerProvider.TryResolve<IIssueCancellationCoordinator>(out var coordinator) &&
            !coordinator.BeforeCancellation(__instance, log, out __state.Owner)) return false;

        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeIssueOutcome(__instance, IssueOutcome.QuestCancel, out __state.Observed);
    }

    [HarmonyPostfix]
    private static void Postfix((IssueBase Observed, IDisposable Owner) __state)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.After(__state.Observed, IssueOutcome.QuestCancel);
    }

    [HarmonyFinalizer]
    private static void Finalizer((IssueBase Observed, IDisposable Owner) __state) => __state.Owner?.Dispose();
}
