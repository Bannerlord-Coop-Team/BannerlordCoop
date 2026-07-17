using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Static bridge that lets the (GameInterface) battle-spawn Harmony patches know whether a coop field battle is
/// the active mission (and which map event it is): the Missions battle stack marks one active on entry
/// (<see cref="BeginBattle"/>) and clears it on exit (<see cref="EndBattle"/>). The patches are static methods
/// that cannot resolve DI services, hence this static bridge.
/// <para>
/// It holds NO host/ownership state: which client fields (spawns) a party is decided server-side by the troop
/// reserve assignment — each client's <c>CoopTroopSupplier</c> only contains the troops it owns, so there is no
/// host-based spawn suppression to gate here. The live host source for the controller is <c>IBattleHostRegistry</c>.
/// </para>
/// </summary>
public static class BattleSpawnGate
{
    private const int MaxRoutedPlayerHitNotifications = 64;
    private static readonly long RoutedPlayerHitNotificationLifetime = Stopwatch.Frequency * 10L;

    private static readonly object Gate = new object();
    private static readonly Queue<CombatLogContext> CombatLogContexts = new Queue<CombatLogContext>();
    private static readonly List<RoutedPlayerHitNotification> RoutedPlayerHitNotifications = new List<RoutedPlayerHitNotification>();
    private static string _activeMapEventId;
    private static bool _defenderReserveTimedOut;
    private static bool _attackerReserveTimedOut;

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

    /// <summary>True while a coop field battle is the active mission.</summary>
    public static bool IsCoopBattleActive
    {
        get { lock (Gate) { return _activeMapEventId != null; } }
    }

    /// <summary>
    /// Set by the live battle's <c>BattleDamageRouter</c>: resolves whether a MOUNT agent is registered in the
    /// battle's agent registry and, if so, whether its authority is remote. <c>true</c> → the horse is another
    /// client's — <c>BattleBlowInterceptPatch</c> suppresses the local blow and routes it (even for a masterless
    /// horse, whose rider-based gate would otherwise apply it locally and diverge). <c>false</c> → the horse is
    /// ours — apply locally. <c>null</c> (or no probe installed) → unregistered — the patch falls back to
    /// rider-keyed gating. Process-global like the rest of this gate: one live battle per game process.
    /// </summary>
    public static Func<Agent, bool?> MountAuthorityProbe { get; set; }

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

    public static string ActiveMapEventId
    {
        get { lock (Gate) { return _activeMapEventId; } }
    }

    /// <summary>
    /// Marks a side whose reserve never arrived before the spawn handler's explicit fallback deadline.
    /// Battle-end checks may treat that side as intentionally absent once deployment is active; an ordinary
    /// not-yet-spawned side is never marked and remains protected from premature depletion.
    /// </summary>
    public static void AcceptMissingReserveSide(BattleSideEnum side)
    {
        lock (Gate)
        {
            if (side == BattleSideEnum.Defender) _defenderReserveTimedOut = true;
            else if (side == BattleSideEnum.Attacker) _attackerReserveTimedOut = true;
        }
    }

    /// <summary>Whether the spawn handler deliberately proceeded without this side's reserve.</summary>
    public static bool IsMissingReserveSideAccepted(BattleSideEnum side)
    {
        lock (Gate)
        {
            if (side == BattleSideEnum.Defender) return _defenderReserveTimedOut;
            if (side == BattleSideEnum.Attacker) return _attackerReserveTimedOut;
            return false;
        }
    }

    /// <summary>[Controller] Mark a coop battle active.</summary>
    public static void BeginBattle(string mapEventId)
    {
        lock (Gate)
        {
            _activeMapEventId = mapEventId;
            _defenderReserveTimedOut = false;
            _attackerReserveTimedOut = false;
            CombatLogContexts.Clear();
            RoutedPlayerHitNotifications.Clear();
        }
    }

    /// <summary>[Controller] Clear on mission end.</summary>
    public static void EndBattle()
    {
        lock (Gate)
        {
            _activeMapEventId = null;
            _defenderReserveTimedOut = false;
            _attackerReserveTimedOut = false;
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
