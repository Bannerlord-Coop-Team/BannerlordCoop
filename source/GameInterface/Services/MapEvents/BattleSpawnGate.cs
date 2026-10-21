using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Static bridge for the battle patches' remaining per-call state: puppet-spawn capture suppression, routed-blow
/// and replicated-death context, and kill-feed bookkeeping. Whether a coop battle is active lives in
/// <see cref="ICoopBattleMissionRegistry"/>, and agent ownership in <see cref="IBattleAgentAuthorityRegistry"/>.
/// </summary>
public static class BattleSpawnGate
{
    private const int MaxRoutedPlayerHitNotifications = 64;
    private static readonly long RoutedPlayerHitNotificationLifetime = Stopwatch.Frequency * 10L;

    private static readonly object Gate = new object();
    private static readonly Queue<CombatLogContext> CombatLogContexts = new Queue<CombatLogContext>();
    private static readonly List<RoutedPlayerHitNotification> RoutedPlayerHitNotifications = new List<RoutedPlayerHitNotification>();

    [System.ThreadStatic]
    private static bool _suppressCapture;

    [System.ThreadStatic]
    private static Agent _replicatedDeathAgent;

    [System.ThreadStatic]
    private static Agent _replicatedDeathAffector;

    [System.ThreadStatic]
    private static KillingBlow _replicatedKillingBlow;

    [System.ThreadStatic]
    private static AgentState _replicatedDeathState;

    [System.ThreadStatic]
    private static Agent _currentRoutedPlayerHitAgent;

    [System.ThreadStatic]
    private static int _currentRoutedPlayerHitDamage;

    [System.ThreadStatic]
    private static WeaponComponentData _routedAttackerWeapon;

    [System.ThreadStatic]
    private static string _routedBlowSourceControllerId;

    /// <summary>
    /// Set around a puppet spawn (<c>CoopBattleController.SpawnPuppet</c>) so the spawn-capture patch does NOT
    /// re-capture and re-broadcast it — only locally owned native spawns should be captured. Thread-local: it
    /// is set and read on the game thread within a single <c>Mission.SpawnAgent</c> call.
    /// </summary>
    public static bool SuppressCapture
    {
        get => _suppressCapture;
        set => _suppressCapture = value;
    }

    /// <summary>Exposes the routed blow's weapon and originating peer during native damage and sound playback.</summary>
    public static void RunWithRoutedBlow(
        WeaponComponentData attackerWeapon, string sourceControllerId, Action applyBlow)
    {
        var previousWeapon = _routedAttackerWeapon;
        string previousSource = _routedBlowSourceControllerId;
        _routedAttackerWeapon = attackerWeapon;
        _routedBlowSourceControllerId = sourceControllerId;
        try
        {
            applyBlow();
        }
        finally
        {
            _routedAttackerWeapon = previousWeapon;
            _routedBlowSourceControllerId = previousSource;
        }
    }

    public static WeaponComponentData RoutedAttackerWeapon => _routedAttackerWeapon;
    public static string RoutedBlowSourceControllerId => _routedBlowSourceControllerId;

    /// <summary>Runs a replicated puppet death with the owner's kill-feed metadata available to UI patches.</summary>
    public static void RunWithReplicatedDeath(
        Agent affectedAgent,
        Agent affectorAgent,
        KillingBlow killingBlow,
        AgentState agentState,
        Action applyDeath)
    {
        var previousAgent = _replicatedDeathAgent;
        var previousAffector = _replicatedDeathAffector;
        var previousKillingBlow = _replicatedKillingBlow;
        var previousAgentState = _replicatedDeathState;

        _replicatedDeathAgent = affectedAgent;
        _replicatedDeathAffector = affectorAgent;
        _replicatedKillingBlow = killingBlow;
        _replicatedDeathState = agentState;
        try
        {
            applyDeath();
        }
        finally
        {
            _replicatedDeathAgent = previousAgent;
            _replicatedDeathAffector = previousAffector;
            _replicatedKillingBlow = previousKillingBlow;
            _replicatedDeathState = previousAgentState;
        }
    }

    public static bool IsReplicatedDeath(Agent affectedAgent)
    {
        return ReferenceEquals(_replicatedDeathAgent, affectedAgent);
    }

    public static bool TryGetReplicatedDeath(
        Agent affectedAgent,
        out Agent affectorAgent,
        out KillingBlow killingBlow)
    {
        if (!IsReplicatedDeath(affectedAgent))
        {
            affectorAgent = null;
            killingBlow = default;
            return false;
        }

        affectorAgent = _replicatedDeathAffector;
        killingBlow = _replicatedKillingBlow;
        return true;
    }

    public static bool TryGetReplicatedDeathState(
        Agent affectedAgent,
        out AgentState agentState)
    {
        if (!IsReplicatedDeath(affectedAgent))
        {
            agentState = AgentState.None;
            return false;
        }

        agentState = _replicatedDeathState;
        return true;
    }

    public static void EnqueueCombatLogContext(Agent routedPlayerHitAgent, int damage)
    {
        lock (Gate)
        {
            CombatLogContexts.Enqueue(new CombatLogContext(routedPlayerHitAgent, damage));
        }
    }

    public static void BeginCombatLogEnqueue()
    {
        Monitor.Enter(Gate);
    }

    public static void EndCombatLogEnqueue()
    {
        Monitor.Exit(Gate);
    }

    public static void BeginCombatLog()
    {
        lock (Gate)
        {
            if (CombatLogContexts.Count == 0)
            {
                _currentRoutedPlayerHitAgent = null;
                _currentRoutedPlayerHitDamage = 0;
                return;
            }

            var context = CombatLogContexts.Dequeue();
            _currentRoutedPlayerHitAgent = context.RoutedPlayerHitAgent;
            _currentRoutedPlayerHitDamage = context.Damage;
        }
    }

    public static void EndCombatLog()
    {
        _currentRoutedPlayerHitAgent = null;
        _currentRoutedPlayerHitDamage = 0;
    }

    public static bool TryGetCurrentRoutedPlayerHit(out Agent affectedAgent, out int damage)
    {
        affectedAgent = _currentRoutedPlayerHitAgent;
        damage = _currentRoutedPlayerHitDamage;
        return affectedAgent != null;
    }

    public static void TrackRoutedPlayerHitNotification(Agent affectedAgent, int damage, Action removeNotification)
    {
        Action notificationToRemove = null;
        lock (Gate)
        {
            RemoveExpiredRoutedPlayerHitNotifications();
            for (int i = RoutedPlayerHitNotifications.Count - 1; i >= 0; i--)
            {
                var notification = RoutedPlayerHitNotifications[i];
                if (!notification.IsFatal
                    || !ReferenceEquals(notification.AffectedAgent, affectedAgent)
                    || notification.Damage != damage)
                {
                    continue;
                }

                notificationToRemove = removeNotification;
                RoutedPlayerHitNotifications.RemoveAt(i);
                break;
            }

            if (notificationToRemove == null)
            {
                RoutedPlayerHitNotifications.Add(new RoutedPlayerHitNotification(
                    affectedAgent,
                    damage,
                    removeNotification,
                    isFatal: false));
                TrimRoutedPlayerHitNotifications();
            }
        }

        notificationToRemove?.Invoke();
    }

    public static void RemoveRoutedPlayerHitNotification(Agent affectedAgent, int damage)
    {
        Action notificationToRemove = null;
        lock (Gate)
        {
            RemoveExpiredRoutedPlayerHitNotifications();
            for (int i = RoutedPlayerHitNotifications.Count - 1; i >= 0; i--)
            {
                var notification = RoutedPlayerHitNotifications[i];
                if (notification.IsFatal
                    || !ReferenceEquals(notification.AffectedAgent, affectedAgent)
                    || notification.Damage != damage)
                {
                    continue;
                }

                notificationToRemove = notification.RemoveNotification;
                RoutedPlayerHitNotifications.RemoveAt(i);
                break;
            }

            if (notificationToRemove == null)
            {
                RoutedPlayerHitNotifications.Add(new RoutedPlayerHitNotification(
                    affectedAgent,
                    damage,
                    removeNotification: null,
                    isFatal: true));
                TrimRoutedPlayerHitNotifications();
            }
        }

        notificationToRemove?.Invoke();
    }

    /// <summary>Drops kill-feed bookkeeping left from an earlier battle. Every <see cref="ICoopBattleMissionRegistry.BeginBattle"/>
    /// and <see cref="ICoopBattleMissionRegistry.EndBattle"/> call must be paired with it, otherwise the next kill feed shows stale entries.</summary>
    public static void ResetCombatFeedback()
    {
        lock (Gate)
        {
            CombatLogContexts.Clear();
            RoutedPlayerHitNotifications.Clear();
        }

        EndCombatLog();
        _routedAttackerWeapon = null;
    }

    private static void RemoveExpiredRoutedPlayerHitNotifications()
    {
        long oldestAllowedTimestamp = Stopwatch.GetTimestamp() - RoutedPlayerHitNotificationLifetime;
        RoutedPlayerHitNotifications.RemoveAll(notification => notification.RecordedAt < oldestAllowedTimestamp);
    }

    private static void TrimRoutedPlayerHitNotifications()
    {
        if (RoutedPlayerHitNotifications.Count > MaxRoutedPlayerHitNotifications)
            RoutedPlayerHitNotifications.RemoveRange(0, RoutedPlayerHitNotifications.Count - MaxRoutedPlayerHitNotifications);
    }

    private sealed class CombatLogContext
    {
        public CombatLogContext(Agent routedPlayerHitAgent, int damage)
        {
            RoutedPlayerHitAgent = routedPlayerHitAgent;
            Damage = damage;
        }

        public Agent RoutedPlayerHitAgent { get; }
        public int Damage { get; }
    }

    private sealed class RoutedPlayerHitNotification
    {
        public RoutedPlayerHitNotification(
            Agent affectedAgent,
            int damage,
            Action removeNotification,
            bool isFatal)
        {
            AffectedAgent = affectedAgent;
            Damage = damage;
            RemoveNotification = removeNotification;
            IsFatal = isFatal;
            RecordedAt = Stopwatch.GetTimestamp();
        }

        public Agent AffectedAgent { get; }
        public int Damage { get; }
        public Action RemoveNotification { get; }
        public bool IsFatal { get; }
        public long RecordedAt { get; }
    }
}
