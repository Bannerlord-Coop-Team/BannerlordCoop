using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Delegates giver death and cancellation retries to the session service.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class IssueGiverDeathDispatchPatches
{
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool Prefix(Hero victim, out (IssueBase Issue, IDisposable Owner) __state)
    {
        // Kept here, since the cancel clears the giver's issue
        __state = (victim.Issue, null);
        return !ContainerProvider.TryResolve<IIssueCancellationCoordinator>(out var coordinator) ||
            coordinator.BeforeGiverDeath(victim, out __state.Owner);
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyFinalizer]
    private static Exception Finalizer((IssueBase Issue, IDisposable Owner) __state, Exception __exception)
    {
        // Vanilla cancels the quest inline, so a throw there would otherwise leave it unannounced and unwind the death
        if (__exception != null && __state.Owner != null && ContainerProvider.TryResolve<IActiveIssueDismisser>(out var dismisser))
            return dismisser.EndAfterThrow(__state.Issue, IssueOutcome.QuestCancel, __state.Owner, __exception);

        __state.Owner?.Dispose();
        return __exception;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void RetryPostfix()
    {
        if (ContainerProvider.TryResolve<IIssueCancellationCoordinator>(out var coordinator)) coordinator.Retry();
    }
}
