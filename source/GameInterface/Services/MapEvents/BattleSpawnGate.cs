using System;
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
    private static readonly object Gate = new object();
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
    /// The live battle's authority seam, installed by <c>CoopBattleController</c> on entry and cleared on
    /// dispose. It is how the static, DI-less battle patches (e.g. <c>BattleBlowInterceptPatch</c>) reach the
    /// per-mission agent registry to decide whether an agent is ours or a puppet. Null when no coop battle is
    /// active. Process-global like the rest of this gate: one live battle per game process. Replaces the old
    /// <c>MountAuthorityProbe</c> delegate (which only answered the mount branch).
    /// </summary>
    public static IAgentAuthority AgentAuthority { get; set; }

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
        }
    }
}
