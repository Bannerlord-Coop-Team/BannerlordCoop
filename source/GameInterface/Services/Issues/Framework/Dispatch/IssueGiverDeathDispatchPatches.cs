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
    private static bool Prefix(Hero victim, out IDisposable __state)
    {
        __state = null;
        return !ContainerProvider.TryResolve<IIssueCancellationCoordinator>(out var coordinator) ||
            coordinator.BeforeGiverDeath(victim, out __state);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool CampaignEventPrefix(Hero victim, out IDisposable __state)
    {
        __state = null;
        if (ContainerProvider.TryResolve<IIssueCancellationCoordinator>(out var coordinator))
            coordinator.BeforeGiverDeath(victim, out __state);

        // This prefix must not suppress the dispatcher itself. Returning false here
        // skips every vanilla OnHeroKilled receiver, not just IssueManager.
        return true;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyFinalizer]
    private static void CampaignEventFinalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void RetryPostfix()
    {
        if (ContainerProvider.TryResolve<IIssueCancellationCoordinator>(out var coordinator)) coordinator.Retry();
    }
}
