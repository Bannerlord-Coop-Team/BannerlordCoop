namespace GameInterface.Services.MapEvents;

/// <summary>
/// Static bridge that lets the (GameInterface) battle-spawn Harmony patches know, for the battle mission
/// currently open, whether this client is the elected host. The Missions battle stack pushes state in:
/// the controller marks a coop battle active on entry (<see cref="BeginBattle"/>) and clears it on exit
/// (<see cref="EndBattle"/>); the host handler sets the host result once the server's assignment arrives
/// (<see cref="SetLocalHost"/>). The patches can't resolve DI services, hence this static.
/// <para>
/// <see cref="LocalIsHost"/> is a tri-state: null until the host is known. The disable patch treats null
/// as "not host yet" and withholds spawning, so a non-host never spawns its own copy of the battle and the
/// host only begins once it knows it is the host.
/// </para>
/// </summary>
public static class BattleSpawnGate
{
    private static readonly object Gate = new object();
    private static string _activeMapEventId;
    private static bool? _localIsHost;

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

    /// <summary>Whether this client is the host of the active battle; null until the assignment is known.</summary>
    public static bool? LocalIsHost
    {
        get { lock (Gate) { return _localIsHost; } }
    }

    public static string ActiveMapEventId
    {
        get { lock (Gate) { return _activeMapEventId; } }
    }

    /// <summary>[Controller] Mark a coop battle active, seeding the host result if it is already known.</summary>
    public static void BeginBattle(string mapEventId, bool? isHost)
    {
        lock (Gate)
        {
            _activeMapEventId = mapEventId;
            _localIsHost = isHost;
        }
    }

    /// <summary>[Host handler] Record the host result once the server's assignment arrives.</summary>
    public static void SetLocalHost(string mapEventId, bool isHost)
    {
        lock (Gate)
        {
            if (mapEventId != null && mapEventId == _activeMapEventId)
                _localIsHost = isHost;
        }
    }

    /// <summary>[Controller] Clear on mission end.</summary>
    public static void EndBattle()
    {
        lock (Gate)
        {
            _activeMapEventId = null;
            _localIsHost = null;
        }
    }
}
