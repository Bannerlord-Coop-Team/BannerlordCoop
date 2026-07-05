using GameInterface.Services.MapEvents.Handlers;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Client] The single, server-authoritative record of how the local player's current battle is being resolved — a
/// live mission or an auto-resolve simulation. Set from the server's <see cref="Messages.Start.NetworkBattleModeSet"/>
/// broadcast (sent when <c>ServerBattleModeArbiter</c> claims or releases the event) and cleared when the mission is
/// abandoned or the event finalizes. The encounter-menu gate (<c>BattleModeEncounterOptionsPatch</c>) reads it to
/// grey out the wrong-mode options. Replaces the former pair of mission/simulation client flags;
/// <see cref="BattleSimulationReplay"/> keeps only its playback state now.
/// </summary>
/// <remarks>
/// The local player is in at most one battle at a time, so a single id suffices. Touched only on the game main thread.
/// </remarks>
internal static class BattleModeRegistry
{
    private static string mapEventId;
    private static BattleStartMode mode;

    /// <summary>Record the mode the server claimed for the given map event.</summary>
    public static void Begin(string id, BattleStartMode claimedMode)
    {
        mapEventId = id;
        mode = claimedMode;
    }

    /// <summary>Clear the record (the battle has ended / the event finalized).</summary>
    public static void End() => mapEventId = null;

    /// <summary>Clear the record only if it still belongs to the given map event.</summary>
    public static void End(string id)
    {
        if (mapEventId == id)
            mapEventId = null;
    }

    /// <summary>True if a live mission owns the given map event.</summary>
    public static bool IsMission(string id) => mapEventId != null && mapEventId == id && mode == BattleStartMode.Mission;

    /// <summary>True if an auto-resolve simulation owns the given map event.</summary>
    public static bool IsSimulation(string id) => mapEventId != null && mapEventId == id && mode == BattleStartMode.Simulation;
}
