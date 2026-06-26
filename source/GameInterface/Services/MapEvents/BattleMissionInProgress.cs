namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Client] Tracks whether the local player's current battle is being fought as a live mission, as opposed to an
/// auto-resolve simulation (tracked by <see cref="BattleSimulationReplay"/>). Set when the server broadcasts
/// <see cref="Messages.Start.NetworkBattleMissionStarted"/> for the map event this player is in; cleared when that
/// event finalizes. The encounter-menu patch reads it to grey out the auto-resolve options once a mission is
/// underway, enforcing mission-XOR-simulation across every player sharing the event.
/// </summary>
/// <remarks>
/// The local player is in at most one battle at a time, so a single id suffices (mirrors
/// <see cref="BattleSimulationReplay"/>). Touched only on the game main thread.
/// </remarks>
internal static class BattleMissionInProgress
{
    private static string mapEventId;

    /// <summary>True while a live mission is underway for the given map event.</summary>
    public static bool IsActiveFor(string id) => mapEventId != null && mapEventId == id;

    /// <summary>Record that a mission has started for the given map event.</summary>
    public static void Begin(string id) => mapEventId = id;

    /// <summary>Clear the active mission (the battle has ended / the event finalized).</summary>
    public static void End() => mapEventId = null;
}
