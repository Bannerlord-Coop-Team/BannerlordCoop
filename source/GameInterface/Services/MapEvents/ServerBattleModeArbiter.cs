using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Authoritative gate that keeps a map event in a single battle-resolution mode: the first player to start
/// a live mission OR an auto-resolve simulation claims the event, and the other mode is refused until the event is
/// released (finalized). The client-side <c>BattleModeEncounterOptionsPatch</c> greys the menu out for UX; this is
/// the backstop that wins the race when two clients act within the broadcast latency window.
/// </summary>
/// <remarks>
/// Server-only state. Keyed by map-event object-manager id, so concurrent battles on different events are
/// independent. Accessed from both the network thread (request handlers) and the game thread, so guarded by a lock.
/// </remarks>
internal static class ServerBattleModeArbiter
{
    private enum Mode { Mission, Simulation }

    private static readonly object lockObj = new();
    private static readonly Dictionary<string, Mode> modes = new();

    /// <summary>
    /// Try to claim the event for a live mission. Succeeds if the event is unclaimed or already a mission (another
    /// player joining the same mission); fails only if an auto-resolve simulation already owns the event.
    /// </summary>
    public static bool TryClaimMission(string mapEventId) => TryClaim(mapEventId, Mode.Mission);

    /// <summary>
    /// Try to claim the event for an auto-resolve simulation. Succeeds if the event is unclaimed or already a
    /// simulation; fails only if a live mission already owns the event.
    /// </summary>
    public static bool TryClaimSimulation(string mapEventId) => TryClaim(mapEventId, Mode.Simulation);

    private static bool TryClaim(string mapEventId, Mode mode)
    {
        if (mapEventId == null) return true;

        lock (lockObj)
        {
            if (modes.TryGetValue(mapEventId, out var current))
                return current == mode;

            modes[mapEventId] = mode;
            return true;
        }
    }

    /// <summary>Release the event so a later, unrelated battle on it (or a reused id) starts unclaimed.</summary>
    public static void Release(string mapEventId)
    {
        if (mapEventId == null) return;

        lock (lockObj)
        {
            modes.Remove(mapEventId);
        }
    }
}
