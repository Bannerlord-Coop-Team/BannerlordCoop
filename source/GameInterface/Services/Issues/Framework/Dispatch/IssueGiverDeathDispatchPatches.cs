using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Delegates giver death and cancellation retries to the session service.</summary>
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
