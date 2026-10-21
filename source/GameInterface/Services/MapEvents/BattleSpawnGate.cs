using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Static bridge for the battle patches' remaining kill-feed bookkeeping. Whether a coop battle is active lives in
/// <see cref="ICoopBattleMissionRegistry"/>, and per-call spawn, blow and death context in the injected trackers.
/// </summary>
public static class BattleSpawnGate
{
    private const int MaxRoutedPlayerHitNotifications = 64;
    private static readonly long RoutedPlayerHitNotificationLifetime = Stopwatch.Frequency * 10L;

    private static readonly object Gate = new object();
    private static readonly Queue<CombatLogContext> CombatLogContexts = new Queue<CombatLogContext>();
    private static readonly List<RoutedPlayerHitNotification> RoutedPlayerHitNotifications = new List<RoutedPlayerHitNotification>();

    [System.ThreadStatic]
    private static Agent _currentRoutedPlayerHitAgent;

    [System.ThreadStatic]
    private static int _currentRoutedPlayerHitDamage;

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
