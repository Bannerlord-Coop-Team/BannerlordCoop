using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Authoritative gate that keeps a map event in a single battle-resolution mode: the first player to start
/// a live mission OR an auto-resolve simulation claims the event, and the other mode is refused until the claim is
/// released. A mission claim ends when its mission instance becomes empty; either claim ends when the event
/// finalizes. The client-side <c>BattleModeEncounterOptionsPatch</c> greys the menu out for UX; this is the backstop
/// that wins the race when two clients act within the broadcast latency window.
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
    public static bool TryClaimMission(string mapEventId) => TryClaimMission(mapEventId, out _);

    /// <summary>
    /// Try to claim the event for a live mission and report whether this request created the claim rather than
    /// joining an existing mission.
    /// </summary>
    public static bool TryClaimMission(string mapEventId, out bool isNewClaim) =>
        TryClaim(mapEventId, Mode.Mission, out isNewClaim);

    /// <summary>
    /// Try to claim the event for an auto-resolve simulation. Succeeds if the event is unclaimed or already a
    /// simulation; fails only if a live mission already owns the event.
    /// </summary>
    public static bool TryClaimSimulation(string mapEventId) => TryClaim(mapEventId, Mode.Simulation, out _);

    /// <summary>
    /// True while either resolution mode owns the event. Read-only: lets side-effectful actions that would
    /// conclude the battle under the mode's players (e.g. a menu surrender) be refused without disturbing
    /// the claim.
    /// </summary>
    public static bool IsClaimed(string mapEventId)
    {
        if (mapEventId == null) return false;

        lock (lockObj)
        {
            return modes.ContainsKey(mapEventId);
        }
    }

    private static bool TryClaim(string mapEventId, Mode mode, out bool isNewClaim)
    {
        isNewClaim = false;
        if (mapEventId == null) return true;

        lock (lockObj)
        {
            if (modes.TryGetValue(mapEventId, out var current))
                return current == mode;

            modes[mapEventId] = mode;
            isNewClaim = true;
            return true;
        }
    }

    /// <summary>
    /// Release a live-mission claim after the final mission member leaves. Returns false when the event is unclaimed
    /// or already belongs to a simulation, so a duplicate mission departure cannot clear a newer simulation claim.
    /// </summary>
    public static bool ReleaseMission(string mapEventId)
    {
        if (mapEventId == null) return false;

        lock (lockObj)
        {
            if (!modes.TryGetValue(mapEventId, out var current) || current != Mode.Mission)
                return false;

            modes.Remove(mapEventId);
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
