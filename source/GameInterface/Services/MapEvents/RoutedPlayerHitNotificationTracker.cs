using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Pairs the player's routed hits on puppets with their kill-feed notifications, so a replicated death of the
/// same victim can remove the hit notification it supersedes.
/// </summary>
public interface IRoutedPlayerHitNotificationTracker
{
    /// <summary>Takes the combat-log lock until <see cref="EndCombatLogEnqueue"/>, so mirrored entries keep vanilla's queue order.</summary>
    void BeginCombatLogEnqueue();

    /// <summary>Releases the combat-log lock taken by <see cref="BeginCombatLogEnqueue"/>.</summary>
    void EndCombatLogEnqueue();

    /// <summary>Mirrors one vanilla combat-log entry, with its routed victim or null when it is not a routed player hit.</summary>
    void EnqueueCombatLogContext(Agent routedPlayerHitAgent, int damage);

    /// <summary>Dequeues the mirror of the combat log vanilla is about to publish and exposes it on the calling thread until disposed.</summary>
    IDisposable BeginCombatLog();

    /// <summary>Gets the routed victim and damage of the combat log being published on the calling thread.</summary>
    bool TryGetCurrentRoutedPlayerHit(out Agent affectedAgent, out int damage);

    /// <summary>Records a routed hit notification, or removes it at once when the victim's replicated death came first.</summary>
    void TrackRoutedPlayerHitNotification(Agent affectedAgent, int damage, Action removeNotification);

    /// <summary>Removes the hit notification a replicated death supersedes, or remembers the death until the hit arrives.</summary>
    void RemoveRoutedPlayerHitNotification(Agent affectedAgent, int damage);

    /// <summary>
    /// Drops bookkeeping left from an earlier battle. <see cref="ICoopBattleMissionRegistry"/> calls it whenever a
    /// battle window opens or closes, otherwise the next kill feed would show stale entries.
    /// </summary>
    void Reset();
}

/// <inheritdoc cref="IRoutedPlayerHitNotificationTracker"/>
public class RoutedPlayerHitNotificationTracker : IRoutedPlayerHitNotificationTracker
{
    private const int MaxRoutedPlayerHitNotifications = 64;
    private static readonly long RoutedPlayerHitNotificationLifetime = Stopwatch.Frequency * 10L;

    // Vanilla enqueues combat logs into a concurrent queue and drains it on the game thread, so the mirror is locked.
    private readonly object sync = new object();
    private readonly Queue<CombatLogContext> combatLogContexts = new Queue<CombatLogContext>();
    private readonly List<RoutedPlayerHitNotification> routedPlayerHitNotifications = new List<RoutedPlayerHitNotification>();

    // Per thread because the routed hit is only exposed during one synchronous CombatLogManager.GenerateCombatLog call.
    private readonly ThreadLocal<CombatLogContext> currentRoutedPlayerHit = new ThreadLocal<CombatLogContext>();

    /// <inheritdoc/>
    public void BeginCombatLogEnqueue()
    {
        Monitor.Enter(sync);
    }

    /// <inheritdoc/>
    public void EndCombatLogEnqueue()
    {
        Monitor.Exit(sync);
    }

    /// <inheritdoc/>
    public void EnqueueCombatLogContext(Agent routedPlayerHitAgent, int damage)
    {
        lock (sync)
        {
            combatLogContexts.Enqueue(new CombatLogContext(routedPlayerHitAgent, damage));
        }
    }

    /// <inheritdoc/>
    public IDisposable BeginCombatLog()
    {
        CombatLogContext next;
        lock (sync)
        {
            next = combatLogContexts.Count == 0 ? null : combatLogContexts.Dequeue();
        }

        return new ThreadLocalValueScope<CombatLogContext>(currentRoutedPlayerHit, next);
    }

    /// <inheritdoc/>
    public bool TryGetCurrentRoutedPlayerHit(out Agent affectedAgent, out int damage)
    {
        var current = currentRoutedPlayerHit.Value;
        affectedAgent = current?.RoutedPlayerHitAgent;
        damage = current?.Damage ?? 0;
        return affectedAgent != null;
    }

    /// <inheritdoc/>
    public void TrackRoutedPlayerHitNotification(Agent affectedAgent, int damage, Action removeNotification)
    {
        bool deathArrivedFirst;
        lock (sync)
        {
            RemoveExpiredRoutedPlayerHitNotifications();
            deathArrivedFirst = TryTakeNotification(affectedAgent, damage, isFatal: true, out _);
            if (!deathArrivedFirst)
                AddNotification(new RoutedPlayerHitNotification(affectedAgent, damage, removeNotification, isFatal: false));
        }

        if (deathArrivedFirst)
            removeNotification?.Invoke();
    }

    /// <inheritdoc/>
    public void RemoveRoutedPlayerHitNotification(Agent affectedAgent, int damage)
    {
        Action notificationToRemove = null;
        lock (sync)
        {
            RemoveExpiredRoutedPlayerHitNotifications();
            if (TryTakeNotification(affectedAgent, damage, isFatal: false, out var hitNotification))
                notificationToRemove = hitNotification.RemoveNotification;
            else
                AddNotification(new RoutedPlayerHitNotification(affectedAgent, damage, removeNotification: null, isFatal: true));
        }

        notificationToRemove?.Invoke();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (sync)
        {
            combatLogContexts.Clear();
            routedPlayerHitNotifications.Clear();
        }
    }

    // Removes and returns the newest notification of the given kind for this victim and damage. Caller holds sync.
    private bool TryTakeNotification(Agent affectedAgent, int damage, bool isFatal, out RoutedPlayerHitNotification match)
    {
        for (int i = routedPlayerHitNotifications.Count - 1; i >= 0; i--)
        {
            var notification = routedPlayerHitNotifications[i];
            if (notification.IsFatal != isFatal
                || !ReferenceEquals(notification.AffectedAgent, affectedAgent)
                || notification.Damage != damage)
            {
                continue;
            }

            match = notification;
            routedPlayerHitNotifications.RemoveAt(i);
            return true;
        }

        match = null;
        return false;
    }

    // Records an unmatched notification, keeping only the newest entries. Caller holds sync.
    private void AddNotification(RoutedPlayerHitNotification notification)
    {
        routedPlayerHitNotifications.Add(notification);
        if (routedPlayerHitNotifications.Count > MaxRoutedPlayerHitNotifications)
            routedPlayerHitNotifications.RemoveRange(0, routedPlayerHitNotifications.Count - MaxRoutedPlayerHitNotifications);
    }

    // Drops notifications older than the lifetime, so an unmatched hit or death cannot pair with a much later one. Caller holds sync.
    private void RemoveExpiredRoutedPlayerHitNotifications()
    {
        long oldestAllowedTimestamp = Stopwatch.GetTimestamp() - RoutedPlayerHitNotificationLifetime;
        routedPlayerHitNotifications.RemoveAll(notification => notification.RecordedAt < oldestAllowedTimestamp);
    }

    /// <summary>The routed victim, if any, and damage mirrored for one vanilla combat-log entry.</summary>
    private sealed class CombatLogContext
    {
        // Captures one mirrored combat-log entry.
        public CombatLogContext(Agent routedPlayerHitAgent, int damage)
        {
            RoutedPlayerHitAgent = routedPlayerHitAgent;
            Damage = damage;
        }

        public Agent RoutedPlayerHitAgent { get; }
        public int Damage { get; }
    }

    /// <summary>An unmatched routed hit notification, or a replicated death waiting for its hit notification.</summary>
    private sealed class RoutedPlayerHitNotification
    {
        // Captures the victim, damage and removal callback, stamped with the time it was recorded.
        public RoutedPlayerHitNotification(Agent affectedAgent, int damage, Action removeNotification, bool isFatal)
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
