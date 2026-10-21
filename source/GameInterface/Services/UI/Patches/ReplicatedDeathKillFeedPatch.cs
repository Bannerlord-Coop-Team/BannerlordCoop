using System;
using Common.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.KillFeed.Personal;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Supplies the authoritative attacker and killing blow while the kill feed handles a replicated death.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletKillNotificationSingleplayerUIHandler),
    nameof(MissionGauntletKillNotificationSingleplayerUIHandler.OnAgentRemoved),
    new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) })]
internal class ReplicatedDeathKillFeedPatch
{
    [HarmonyPrefix]
    internal static void Prefix(
        Agent affectedAgent,
        ref Agent affectorAgent,
        ref KillingBlow killingBlow)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!ContainerProvider.TryResolve<ICoopBattleMissionRegistry>(out var missionRegistry)
            || !missionRegistry.IsCoopBattleActive) return;
        if (!ContainerProvider.TryResolve<IReplicatedDeathTracker>(out var replicatedDeathTracker)
            || !replicatedDeathTracker.TryGetReplicatedDeath(affectedAgent, out var replicatedAffector, out var replicatedKillingBlow)) return;

        if (replicatedAffector == Agent.Main
            && affectedAgent.IsHuman
            && replicatedKillingBlow.IsValid
            && ContainerProvider.TryResolve<IRoutedPlayerHitNotificationTracker>(out var notificationTracker))
            notificationTracker.RemoveRoutedPlayerHitNotification(affectedAgent, replicatedKillingBlow.InflictedDamage);

        if (replicatedAffector != null)
            affectorAgent = replicatedAffector;
        if (replicatedKillingBlow.IsValid)
            killingBlow = replicatedKillingBlow;
    }
}

/// <summary>Binds a personal damage notification to the exact routed victim agent.</summary>
[HarmonyPatch(typeof(SPPersonalKillNotificationVM), nameof(SPPersonalKillNotificationVM.OnPersonalHit))]
internal class RoutedPlayerHitNotificationPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        SPPersonalKillNotificationVM __instance,
        int damageAmount,
        bool isMountDamage)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (isMountDamage || __instance.NotificationList.Count == 0) return;
        if (!ContainerProvider.TryResolve<IRoutedPlayerHitNotificationTracker>(out var notificationTracker)
            || !notificationTracker.TryGetCurrentRoutedPlayerHit(out var affectedAgent, out int routedDamage)) return;
        if (!ContainerProvider.TryResolve<ICoopBattleMissionRegistry>(out var missionRegistry)
            || !missionRegistry.IsCoopBattleActive) return;
        if (routedDamage != damageAmount) return;

        var notification = __instance.NotificationList[__instance.NotificationList.Count - 1];
        notificationTracker.TrackRoutedPlayerHitNotification(affectedAgent, damageAmount, notification.ExecuteRemove);
    }
}

/// <summary>Mirrors vanilla combat-log queue entries with their routed victim context.</summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.AddCombatLogSafe))]
internal class RoutedPlayerHitCombatLogPatch
{
    // Holds the tracker's lock across vanilla's enqueue, because AddCombatLogSafe can run on several threads.
    [HarmonyPrefix]
    internal static void Prefix(out IRoutedPlayerHitNotificationTracker __state)
    {
        __state = null;
        if (!BattleSpawnConfig.Enabled) return;
        if (!ContainerProvider.TryResolve<ICoopBattleMissionRegistry>(out var missionRegistry)
            || !missionRegistry.IsCoopBattleActive) return;
        if (!ContainerProvider.TryResolve(out __state)) return;

        __state.BeginCombatLogEnqueue();
    }

    // Mirrors the entry vanilla just enqueued, marking it when it is the player's hit on a routed puppet.
    [HarmonyPostfix]
    internal static void Postfix(Agent attackerAgent, Agent victimAgent, CombatLogData combatLog, IRoutedPlayerHitNotificationTracker __state)
    {
        if (__state == null) return;

        bool isRoutedPlayerHit = !AllowedThread.IsThisThreadAllowed()
            && attackerAgent == Agent.Main
            && victimAgent != null
            && victimAgent.IsHuman
            && victimAgent.Controller == AgentControllerType.None
            && !combatLog.IsFatalDamage
            && combatLog.TotalDamage > 0;

        __state.EnqueueCombatLogContext(isRoutedPlayerHit ? victimAgent : null, combatLog.TotalDamage);
    }

    // Releases the lock even when vanilla's enqueue throws.
    [HarmonyFinalizer]
    internal static Exception Finalizer(Exception __exception, IRoutedPlayerHitNotificationTracker __state)
    {
        __state?.EndCombatLogEnqueue();
        return __exception;
    }
}

/// <summary>Exposes routed victim context while vanilla publishes each queued combat log.</summary>
[HarmonyPatch(typeof(CombatLogManager), nameof(CombatLogManager.GenerateCombatLog))]
internal class RoutedPlayerHitCombatLogContextPatch
{
    // Exposes the mirror of the combat log vanilla is about to publish.
    [HarmonyPrefix]
    internal static void Prefix(out IRoutedPlayerHitNotificationTracker __state)
    {
        if (!ContainerProvider.TryResolve(out __state)) return;

        __state.BeginCombatLog();
    }

    // Clears the exposed routed hit once vanilla has published the log.
    [HarmonyPostfix]
    internal static void Postfix(IRoutedPlayerHitNotificationTracker __state)
    {
        __state?.EndCombatLog();
    }
}
