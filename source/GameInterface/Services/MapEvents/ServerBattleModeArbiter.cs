using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// The outcome of a battle-mode claim attempt: the event became newly claimed for the requested mode, it was
/// already claimed for the same mode (a joiner starting against an already-claimed event — the caller decides
/// whether to proceed), or a different mode already owns it and the claim is refused.
/// </summary>
internal enum BattleClaimResult
{
    NewClaim,
    AlreadyClaimedSameMode,
    Refused,
}

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
    /// Claim the event for a live mission, reporting whether the claim is new, a same-mode re-claim (a player
    /// joining the same mission), or refused because an auto-resolve simulation already owns the event.
    /// </summary>
    public static BattleClaimResult ClaimMission(string mapEventId) => Claim(mapEventId, Mode.Mission);

    /// <summary>
    /// Claim the event for an auto-resolve simulation, reporting whether the claim is new, a same-mode re-claim, or
    /// refused because a live mission already owns the event.
    /// </summary>
    public static BattleClaimResult ClaimSimulation(string mapEventId) => Claim(mapEventId, Mode.Simulation);

    /// <summary>
    /// Try to claim the event for a live mission. Succeeds if the event is unclaimed or already a mission (another
    /// player joining the same mission); fails only if an auto-resolve simulation already owns the event.
    /// </summary>
    public static bool TryClaimMission(string mapEventId) => ClaimMission(mapEventId) != BattleClaimResult.Refused;

    /// <summary>
    /// Try to claim the event for an auto-resolve simulation. Succeeds if the event is unclaimed or already a
    /// simulation; fails only if a live mission already owns the event.
    /// </summary>
    public static bool TryClaimSimulation(string mapEventId) => ClaimSimulation(mapEventId) != BattleClaimResult.Refused;

    private static BattleClaimResult Claim(string mapEventId, Mode mode)
    {
        // A null id has no event to gate; treat it as a fresh claim (matches the historical "return true").
        if (mapEventId == null) return BattleClaimResult.NewClaim;

        lock (lockObj)
        {
            if (modes.TryGetValue(mapEventId, out var current))
                return current == mode ? BattleClaimResult.AlreadyClaimedSameMode : BattleClaimResult.Refused;

            modes[mapEventId] = mode;
            return BattleClaimResult.NewClaim;
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
