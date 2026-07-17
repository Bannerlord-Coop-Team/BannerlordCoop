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
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        if (!BattleSpawnGate.TryGetReplicatedDeath(affectedAgent, out var replicatedAffector, out var replicatedKillingBlow)) return;

        if (replicatedAffector == Agent.Main && affectedAgent.IsHuman && replicatedKillingBlow.IsValid)
            BattleSpawnGate.RemoveRoutedPlayerHitNotification(affectedAgent, replicatedKillingBlow.InflictedDamage);

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
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        if (isMountDamage || __instance.NotificationList.Count == 0) return;
        if (!BattleSpawnGate.TryGetCurrentRoutedPlayerHit(out var affectedAgent, out int routedDamage)) return;
        if (routedDamage != damageAmount) return;

        var notification = __instance.NotificationList[__instance.NotificationList.Count - 1];
        BattleSpawnGate.TrackRoutedPlayerHitNotification(affectedAgent, damageAmount, notification.ExecuteRemove);
    }
}

/// <summary>Mirrors vanilla combat-log queue entries with their routed victim context.</summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.AddCombatLogSafe))]
internal class RoutedPlayerHitCombatLogPatch
{
    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = BattleSpawnConfig.Enabled && BattleSpawnGate.IsCoopBattleActive;
        if (__state)
            BattleSpawnGate.BeginCombatLogEnqueue();
    }

    [HarmonyPostfix]
    private static void Postfix(Agent attackerAgent, Agent victimAgent, CombatLogData combatLog, bool __state)
    {
        if (!__state) return;

        bool isRoutedPlayerHit = !AllowedThread.IsThisThreadAllowed()
            && attackerAgent == Agent.Main
            && victimAgent != null
            && victimAgent.IsHuman
            && victimAgent.Controller == AgentControllerType.None
            && !combatLog.IsFatalDamage
            && combatLog.TotalDamage > 0;

        BattleSpawnGate.EnqueueCombatLogContext(isRoutedPlayerHit ? victimAgent : null, combatLog.TotalDamage);
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state)
            BattleSpawnGate.EndCombatLogEnqueue();
        return __exception;
    }
}

/// <summary>Exposes routed victim context while vanilla publishes each queued combat log.</summary>
[HarmonyPatch(typeof(CombatLogManager), nameof(CombatLogManager.GenerateCombatLog))]
internal class RoutedPlayerHitCombatLogContextPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        BattleSpawnGate.BeginCombatLog();
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        BattleSpawnGate.EndCombatLog();
    }
}
