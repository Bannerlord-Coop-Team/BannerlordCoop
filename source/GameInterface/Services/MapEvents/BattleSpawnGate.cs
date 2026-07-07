using System;
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

    [System.ThreadStatic]
    private static bool _suppressCapture;

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

    public static string ActiveMapEventId
    {
        get { lock (Gate) { return _activeMapEventId; } }
    }

    /// <summary>[Controller] Mark a coop battle active.</summary>
    public static void BeginBattle(string mapEventId)
    {
        lock (Gate)
        {
            _activeMapEventId = mapEventId;
        }
    }

    /// <summary>[Controller] Clear on mission end.</summary>
    public static void EndBattle()
    {
        lock (Gate)
        {
            _activeMapEventId = null;
        }
    }
}
