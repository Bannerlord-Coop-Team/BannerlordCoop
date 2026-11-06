using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The NavalDLC side of an in-mission ship capture: vanilla's vacancy and takeover rules on a hull and vanilla's swap of two
/// hulls' formations. Implemented in Missions.Naval, so Missions binds to no DLC type.
/// </summary>
public interface INavalShipCaptureEngine
{
    /// <summary>
    /// [Game thread] Replays vanilla's capture swap (NavalShipsLogic.OnShipCaptured): <paramref name="prize"/> takes the formation,
    /// team and controller of <paramref name="captorHull"/>, which is left with none. A copy keeps no local controller.
    /// </summary>
    CaptureSwapResult ApplyCapture(MissionObject prize, MissionObject captorHull);

    /// <summary>
    /// [Game thread] Why vanilla's swap can't run on the two hulls here (a short reason), or null when it can. It checks the known
    /// prerequisites only: the swap isn't transactional, so it can still fail part way (<see cref="CaptureSwapResult.Failed"/>).
    /// </summary>
    string CheckCapture(MissionObject prize, MissionObject captorHull);

    /// <summary>[Game thread] Leaves a late joiner's copy of a capture's orphan as vanilla's swap left it: no formation, no team.</summary>
    void OrphanHull(MissionObject hull);

    /// <summary>
    /// [Game thread] Vanilla's capture vacancy on a hull this client simulates, read fresh: its formation is all swimming or gone
    /// and nothing is queued.
    /// </summary>
    bool IsVacantForCapture(MissionObject hull);

    /// <summary>Whether vanilla lets the hull be captured at all (storyline missions turn it off).</summary>
    bool CanBeTakenOver(MissionObject hull);
}

/// <summary>How a replay of vanilla's capture swap ended, so the capture's owner records only a swap that completed.</summary>
public enum CaptureSwapResult
{
    /// <summary>The swap ran to the end.</summary>
    Applied,

    /// <summary>A prerequisite was missing, so nothing of the swap changed.</summary>
    Refused,

    /// <summary>The swap threw after it started changing the hulls, so they are left part way through it.</summary>
    Failed,
}
