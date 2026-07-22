using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>A server-assigned transfer that reserves the next open human-agent slot for a waiting party.</summary>
public readonly struct BattlePrioritySpawnAssignment
{
    public BattlePrioritySpawnAssignment(
        long transferId,
        string waitingPartyId,
        string donorPartyId)
    {
        TransferId = transferId;
        WaitingPartyId = waitingPartyId;
        DonorPartyId = donorPartyId;
    }

    public long TransferId { get; }
    public string WaitingPartyId { get; }
    public string DonorPartyId { get; }
}

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
    private static readonly Dictionary<string, HashSet<string>> PendingPrioritySpawns = new Dictionary<string, HashSet<string>>();
    private static readonly Dictionary<string, Dictionary<long, BattlePrioritySpawnAssignment>> PrioritySpawnAssignments =
        new Dictionary<string, Dictionary<long, BattlePrioritySpawnAssignment>>();
    private static readonly Dictionary<string, HashSet<long>> ConsumedPrioritySpawnTransfers =
        new Dictionary<string, HashSet<long>>();
    private static readonly Dictionary<string, HashSet<string>> RegisteredPrioritySpawnParties =
        new Dictionary<string, HashSet<string>>();
    private static string _activeMapEventId;
    private static int _battleSize;
    private static bool _defenderReserveTimedOut;
    private static bool _attackerReserveTimedOut;

    [System.ThreadStatic]
    private static bool _suppressCapture;

    [System.ThreadStatic]
    private static Agent _replicatedDeathAgent;

    [System.ThreadStatic]
    private static Agent _administrativeRemovalAgent;

    [System.ThreadStatic]
    private static Agent _administrativeRemovalMount;

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

    /// <summary>Temporarily exposes a routed missile's serialized weapon while vanilla calculates hit rewards.</summary>
    public static void RunWithRoutedAttackerWeapon(WeaponComponentData attackerWeapon, Action applyBlow)
    {
        var previousWeapon = _routedAttackerWeapon;
        _routedAttackerWeapon = attackerWeapon;
        try
        {
            applyBlow();
        }
        finally
        {
            _routedAttackerWeapon = previousWeapon;
        }
    }

    public static WeaponComponentData RoutedAttackerWeapon => _routedAttackerWeapon;

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

    /// <summary>Removes an uncommitted duplicate without reporting a routed troop or rebroadcasting its removal.</summary>
    public static void RunWithAdministrativeRemoval(Agent agent, Agent spawnMount, Action remove)
    {
        var previousAgent = _administrativeRemovalAgent;
        var previousMount = _administrativeRemovalMount;
        _administrativeRemovalAgent = agent;
        _administrativeRemovalMount = spawnMount;
        try
        {
            remove();
        }
        finally
        {
            _administrativeRemovalAgent = previousAgent;
            _administrativeRemovalMount = previousMount;
        }
    }

    public static bool IsAdministrativeRemoval(Agent agent)
    {
        return ReferenceEquals(_administrativeRemovalAgent, agent)
            || ReferenceEquals(_administrativeRemovalMount, agent);
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

    /// <summary>The server-authoritative human-agent budget frozen for the active battle.</summary>
    public static int BattleSize
    {
        get { lock (Gate) { return _battleSize; } }
    }

    /// <summary>Queues a party that must receive the next server-assigned human-agent slot.</summary>
    public static void QueuePrioritySpawn(string mapEventId, string partyId)
    {
        if (string.IsNullOrEmpty(mapEventId) || string.IsNullOrEmpty(partyId)) return;

        lock (Gate)
        {
            if (IsPrioritySpawnRegistered(mapEventId, partyId))
                return;
            GetOrCreatePendingPrioritySpawns(mapEventId).Add(partyId);
        }
    }

    /// <summary>Clears stale transfer state for a reconnecting party and queues it unless its puppet arrived.</summary>
    public static void ResetAndQueuePrioritySpawn(string mapEventId, string partyId)
    {
        if (string.IsNullOrEmpty(mapEventId) || string.IsNullOrEmpty(partyId)) return;

        lock (Gate)
        {
            RemovePrioritySpawnAssignments(mapEventId, partyId, consumedOnly: false);
            if (!IsPrioritySpawnRegistered(mapEventId, partyId))
                GetOrCreatePendingPrioritySpawns(mapEventId).Add(partyId);
        }
    }

    /// <summary>Replaces transient transfer state before an authoritative entry snapshot is replayed.</summary>
    public static void ResetPrioritySpawnSnapshot(string mapEventId)
    {
        if (string.IsNullOrEmpty(mapEventId)) return;

        lock (Gate)
        {
            ClearPrioritySpawnSnapshotState(mapEventId);
        }
    }

    /// <summary>Records the donor slot selected by the server for a waiting party.</summary>
    public static void RecordPrioritySpawnAssignment(
        string mapEventId,
        long transferId,
        string waitingPartyId,
        string donorPartyId)
    {
        if (string.IsNullOrEmpty(mapEventId)
            || transferId <= 0
            || string.IsNullOrEmpty(waitingPartyId)
            || string.IsNullOrEmpty(donorPartyId))
        {
            return;
        }

        lock (Gate)
        {
            bool isRegistered = IsPrioritySpawnRegistered(mapEventId, waitingPartyId);
            if (!PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments))
            {
                assignments = new Dictionary<long, BattlePrioritySpawnAssignment>();
                PrioritySpawnAssignments[mapEventId] = assignments;
            }

            var staleTransfers = new List<long>();
            foreach (var assignment in assignments)
            {
                if (assignment.Key != transferId
                    && assignment.Value.WaitingPartyId == waitingPartyId)
                {
                    staleTransfers.Add(assignment.Key);
                }
            }
            foreach (var staleTransferId in staleTransfers)
            {
                assignments.Remove(staleTransferId);
                RemoveConsumedPrioritySpawnTransfer(mapEventId, staleTransferId);
            }

            if (assignments.TryGetValue(transferId, out var previous)
                && previous.WaitingPartyId != waitingPartyId)
            {
                RemovePendingPrioritySpawn(mapEventId, previous.WaitingPartyId);
                RemoveConsumedPrioritySpawnTransfer(mapEventId, transferId);
            }

            assignments[transferId] = new BattlePrioritySpawnAssignment(
                transferId,
                waitingPartyId,
                donorPartyId);
            if (!isRegistered)
                GetOrCreatePendingPrioritySpawns(mapEventId).Add(waitingPartyId);
        }
    }

    /// <summary>Records the server's consumed acknowledgement without releasing the human-slot gate.</summary>
    public static bool MarkPrioritySpawnConsumed(string mapEventId, long transferId, string waitingPartyId)
    {
        if (string.IsNullOrEmpty(mapEventId)
            || transferId <= 0
            || string.IsNullOrEmpty(waitingPartyId))
        {
            return false;
        }

        lock (Gate)
        {
            if (!PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments)
                || !assignments.TryGetValue(transferId, out var assignment)
                || assignment.WaitingPartyId != waitingPartyId)
            {
                return false;
            }

            if (!ConsumedPrioritySpawnTransfers.TryGetValue(mapEventId, out var consumedTransfers))
            {
                consumedTransfers = new HashSet<long>();
                ConsumedPrioritySpawnTransfers[mapEventId] = consumedTransfers;
            }
            consumedTransfers.Add(transferId);
            return true;
        }
    }

    /// <summary>Clears a party's pending marker after its priority spawn is registered locally.</summary>
    public static bool CompletePrioritySpawn(string mapEventId, string partyId)
    {
        if (string.IsNullOrEmpty(mapEventId) || string.IsNullOrEmpty(partyId)) return false;

        lock (Gate)
        {
            bool registered = GetOrCreateRegisteredPrioritySpawns(mapEventId).Add(partyId);
            bool removed = RemovePendingPrioritySpawn(mapEventId, partyId);
            return registered || removed;
        }
    }

    /// <summary>Clears a party that left before its priority spawn was registered.</summary>
    public static bool CancelPrioritySpawn(string mapEventId, string partyId)
    {
        if (string.IsNullOrEmpty(mapEventId) || string.IsNullOrEmpty(partyId)) return false;

        lock (Gate)
        {
            bool removed = RemovePendingPrioritySpawn(mapEventId, partyId);
            return RemovePrioritySpawnAssignments(mapEventId, partyId, consumedOnly: false) || removed;
        }
    }

    /// <summary>Clears a departed party only after the server acknowledged that its slot was consumed.</summary>
    public static bool ClearConsumedPrioritySpawn(string mapEventId, string partyId)
    {
        if (string.IsNullOrEmpty(mapEventId) || string.IsNullOrEmpty(partyId)) return false;

        lock (Gate)
        {
            RemovePrioritySpawnRegistration(mapEventId, partyId);
            bool removed = RemovePrioritySpawnAssignments(mapEventId, partyId, consumedOnly: true);
            if (removed)
                RemovePendingPrioritySpawn(mapEventId, partyId);
            return removed;
        }
    }

    /// <summary>Forgets a consumed transfer whose priority human departed without restoring its donor phase.</summary>
    public static bool SettlePrioritySpawn(string mapEventId, long transferId, string waitingPartyId)
    {
        if (string.IsNullOrEmpty(mapEventId)
            || transferId <= 0
            || string.IsNullOrEmpty(waitingPartyId))
        {
            return false;
        }

        lock (Gate)
        {
            if (!PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments)
                || !assignments.TryGetValue(transferId, out var assignment)
                || assignment.WaitingPartyId != waitingPartyId)
            {
                return false;
            }

            assignments.Remove(transferId);
            if (assignments.Count == 0)
                PrioritySpawnAssignments.Remove(mapEventId);
            RemoveConsumedPrioritySpawnTransfer(mapEventId, transferId);
            RemovePendingPrioritySpawn(mapEventId, waitingPartyId);
            RemovePrioritySpawnRegistration(mapEventId, waitingPartyId);
            return true;
        }
    }

    /// <summary>Clears a departed waiter unless the server has already assigned it a donor slot.</summary>
    public static bool CancelUnassignedPrioritySpawn(string mapEventId, string partyId)
    {
        if (string.IsNullOrEmpty(mapEventId) || string.IsNullOrEmpty(partyId)) return false;

        lock (Gate)
        {
            if (PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments))
            {
                foreach (var assignment in assignments.Values)
                {
                    if (assignment.WaitingPartyId == partyId)
                        return false;
                }
            }

            return RemovePendingPrioritySpawn(mapEventId, partyId);
        }
    }

    /// <summary>Clears a server-cancelled transfer and its waiting-party marker.</summary>
    public static bool CancelPrioritySpawnAssignment(string mapEventId, long transferId)
    {
        if (string.IsNullOrEmpty(mapEventId) || transferId <= 0) return false;

        lock (Gate)
        {
            if (!PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments)
                || !assignments.TryGetValue(transferId, out var assignment))
            {
                return false;
            }

            assignments.Remove(transferId);
            RemoveConsumedPrioritySpawnTransfer(mapEventId, transferId);
            if (assignments.Count == 0)
                PrioritySpawnAssignments.Remove(mapEventId);
            RemovePendingPrioritySpawn(mapEventId, assignment.WaitingPartyId);
            return true;
        }
    }

    /// <summary>Whether the active battle has a player party waiting for its priority spawn.</summary>
    public static bool HasPendingPrioritySpawn
    {
        get
        {
            lock (Gate)
            {
                return _activeMapEventId != null
                    && PendingPrioritySpawns.TryGetValue(_activeMapEventId, out var parties)
                    && parties.Count > 0;
            }
        }
    }

    /// <summary>Returns a stable snapshot of the server's current assignments for a map event.</summary>
    public static IReadOnlyList<BattlePrioritySpawnAssignment> GetPrioritySpawnAssignments(string mapEventId)
    {
        if (string.IsNullOrEmpty(mapEventId)) return Array.Empty<BattlePrioritySpawnAssignment>();

        lock (Gate)
        {
            if (!PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments)
                || assignments.Count == 0)
            {
                return Array.Empty<BattlePrioritySpawnAssignment>();
            }

            var snapshot = new BattlePrioritySpawnAssignment[assignments.Count];
            assignments.Values.CopyTo(snapshot, 0);
            Array.Sort(snapshot, (left, right) => left.TransferId.CompareTo(right.TransferId));
            return snapshot;
        }
    }

    /// <summary>Whether the active coop battle has room for another active human agent.</summary>
    public static bool HasAvailableHumanAgentSlot(Mission mission)
    {
        int battleSize;
        lock (Gate)
        {
            if (_activeMapEventId == null || _battleSize <= 0 || mission == null)
                return false;
            battleSize = _battleSize;
        }

        int activeHumans = 0;
        foreach (var agent in mission.Agents)
        {
            if (agent == null || !agent.IsHuman || !agent.IsActive()) continue;
            activeHumans++;
            if (activeHumans >= battleSize) return false;
        }

        return true;
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
    public static void BeginBattle(string mapEventId, int battleSize)
    {
        if (battleSize <= 0) throw new ArgumentOutOfRangeException(nameof(battleSize));

        lock (Gate)
        {
            if (_activeMapEventId != null && _activeMapEventId != mapEventId)
                ClearPrioritySpawnState(_activeMapEventId);
            _activeMapEventId = mapEventId;
            _battleSize = battleSize;
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
            ClearPrioritySpawnState(_activeMapEventId);
            _activeMapEventId = null;
            _battleSize = 0;
            _defenderReserveTimedOut = false;
            _attackerReserveTimedOut = false;
            CombatLogContexts.Clear();
            RoutedPlayerHitNotifications.Clear();
        }

        EndCombatLog();
        _routedAttackerWeapon = null;
    }

    private static void ClearPrioritySpawnState(string mapEventId)
    {
        if (mapEventId == null) return;
        ClearPrioritySpawnSnapshotState(mapEventId);
        RegisteredPrioritySpawnParties.Remove(mapEventId);
    }

    private static void ClearPrioritySpawnSnapshotState(string mapEventId)
    {
        PendingPrioritySpawns.Remove(mapEventId);
        PrioritySpawnAssignments.Remove(mapEventId);
        ConsumedPrioritySpawnTransfers.Remove(mapEventId);
    }

    private static HashSet<string> GetOrCreateRegisteredPrioritySpawns(string mapEventId)
    {
        if (!RegisteredPrioritySpawnParties.TryGetValue(mapEventId, out var parties))
        {
            parties = new HashSet<string>();
            RegisteredPrioritySpawnParties[mapEventId] = parties;
        }
        return parties;
    }

    private static bool IsPrioritySpawnRegistered(string mapEventId, string partyId)
    {
        return RegisteredPrioritySpawnParties.TryGetValue(mapEventId, out var parties)
            && parties.Contains(partyId);
    }

    private static void RemovePrioritySpawnRegistration(string mapEventId, string partyId)
    {
        if (!RegisteredPrioritySpawnParties.TryGetValue(mapEventId, out var parties))
            return;

        parties.Remove(partyId);
        if (parties.Count == 0)
            RegisteredPrioritySpawnParties.Remove(mapEventId);
    }

    private static bool RemovePrioritySpawnAssignments(
        string mapEventId,
        string partyId,
        bool consumedOnly)
    {
        if (!PrioritySpawnAssignments.TryGetValue(mapEventId, out var assignments))
            return false;

        ConsumedPrioritySpawnTransfers.TryGetValue(mapEventId, out var consumedTransfers);
        var removedTransfers = new List<long>();
        foreach (var assignment in assignments)
        {
            if (assignment.Value.WaitingPartyId != partyId)
                continue;
            if (consumedOnly && (consumedTransfers == null || !consumedTransfers.Contains(assignment.Key)))
                continue;
            removedTransfers.Add(assignment.Key);
        }

        foreach (var transferId in removedTransfers)
        {
            assignments.Remove(transferId);
            RemoveConsumedPrioritySpawnTransfer(mapEventId, transferId);
        }
        if (assignments.Count == 0)
            PrioritySpawnAssignments.Remove(mapEventId);
        return removedTransfers.Count > 0;
    }

    private static void RemoveConsumedPrioritySpawnTransfer(string mapEventId, long transferId)
    {
        if (!ConsumedPrioritySpawnTransfers.TryGetValue(mapEventId, out var consumedTransfers))
            return;

        consumedTransfers.Remove(transferId);
        if (consumedTransfers.Count == 0)
            ConsumedPrioritySpawnTransfers.Remove(mapEventId);
    }

    private static HashSet<string> GetOrCreatePendingPrioritySpawns(string mapEventId)
    {
        if (!PendingPrioritySpawns.TryGetValue(mapEventId, out var parties))
        {
            parties = new HashSet<string>();
            PendingPrioritySpawns[mapEventId] = parties;
        }
        return parties;
    }

    private static bool RemovePendingPrioritySpawn(string mapEventId, string partyId)
    {
        if (!PendingPrioritySpawns.TryGetValue(mapEventId, out var parties)) return false;

        bool removed = parties.Remove(partyId);
        if (parties.Count == 0)
            PendingPrioritySpawns.Remove(mapEventId);
        return removed;
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
