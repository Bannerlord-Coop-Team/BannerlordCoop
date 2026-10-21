using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Tracks the coop battle windows open on this machine, so GameInterface patches can tell a coop battle apart from
/// a vanilla or mission-less one. The battle window spans entry to leave; the mission window spans load to dispose.
/// </summary>
public interface ICoopBattleMissionRegistry
{
    /// <summary>
    /// True from the moment a coop battle mission finishes loading until its controller is disposed. Coop
    /// mission victories then report through NetworkBattleResultReady instead of the legacy state relay.
    /// </summary>
    bool IsMissionActive { get; }

    /// <summary>
    /// Opens the loaded-mission window for <paramref name="mapEventId"/>, replacing any earlier one. Windows that
    /// share an id (two on one map event, or two null-id windows) cannot be told apart, so they must not overlap.
    /// </summary>
    void BeginMission(string mapEventId);

    /// <summary>Closes the loaded-mission window only if <paramref name="mapEventId"/> (possibly null) still owns it.</summary>
    void EndMission(string mapEventId);

    /// <summary>
    /// True from just before a coop battle mission is built (its initial wave spawns during the build) until the
    /// battle instance is left. Opens earlier than <see cref="IsMissionActive"/> and closes just before it, on leave.
    /// </summary>
    bool IsCoopBattleActive { get; }

    /// <summary>The map-event id of the open battle window, or null when no coop battle is active.</summary>
    string ActiveMapEventId { get; }

    /// <summary>
    /// Opens the battle window for <paramref name="mapEventId"/> and clears any reserve timeouts. Callers also reset the
    /// kill feed with <see cref="IRoutedPlayerHitNotificationTracker.Reset"/>.
    /// </summary>
    void BeginBattle(string mapEventId);

    /// <summary>
    /// Closes the battle window and clears any reserve timeouts. Not keyed like <see cref="EndMission"/>, because its
    /// only callers (leaving the battle, or a failed open) both mean no coop battle should stay active. Callers also
    /// reset the kill feed with <see cref="IRoutedPlayerHitNotificationTracker.Reset"/>.
    /// </summary>
    void EndBattle();

    /// <summary>
    /// Marks a side whose reserve never arrived before the spawn handler's fallback deadline, so battle-end checks
    /// may treat it as intentionally absent. An ordinary not-yet-spawned side is never marked.
    /// </summary>
    void AcceptMissingReserveSide(BattleSideEnum side);

    /// <summary>Clears the timeout fallback when a late authoritative reserve arrives for this side.</summary>
    void RestoreReserveSide(BattleSideEnum side);

    /// <summary>Whether the spawn handler deliberately proceeded without this side's reserve.</summary>
    bool IsMissingReserveSideAccepted(BattleSideEnum side);
}

/// <inheritdoc cref="ICoopBattleMissionRegistry"/>
public class CoopBattleMissionRegistry : ICoopBattleMissionRegistry
{
    private readonly object sync = new();
    private bool isMissionActive;
    private string missionId;
    private string activeMapEventId;
    private bool defenderReserveTimedOut;
    private bool attackerReserveTimedOut;

    /// <inheritdoc/>
    public bool IsMissionActive
    {
        get
        {
            lock (sync)
            {
                return isMissionActive;
            }
        }
    }

    /// <inheritdoc/>
    public void BeginMission(string mapEventId)
    {
        lock (sync)
        {
            isMissionActive = true;
            missionId = mapEventId;
        }
    }

    /// <inheritdoc/>
    // Keyed so a stale controller disposed after a newer mission began cannot close the newer window.
    public void EndMission(string mapEventId)
    {
        lock (sync)
        {
            if (!isMissionActive || missionId != mapEventId) return;

            isMissionActive = false;
            missionId = null;
        }
    }

    /// <inheritdoc/>
    public bool IsCoopBattleActive
    {
        get
        {
            lock (sync)
            {
                return activeMapEventId != null;
            }
        }
    }

    /// <inheritdoc/>
    public string ActiveMapEventId
    {
        get
        {
            lock (sync)
            {
                return activeMapEventId;
            }
        }
    }

    /// <inheritdoc/>
    public void BeginBattle(string mapEventId)
    {
        lock (sync)
        {
            activeMapEventId = mapEventId;
            ClearReserveTimeouts();
        }
    }

    /// <inheritdoc/>
    public void EndBattle()
    {
        lock (sync)
        {
            activeMapEventId = null;
            ClearReserveTimeouts();
        }
    }

    /// <inheritdoc/>
    public void AcceptMissingReserveSide(BattleSideEnum side)
    {
        SetReserveTimedOut(side, timedOut: true);
    }

    /// <inheritdoc/>
    public void RestoreReserveSide(BattleSideEnum side)
    {
        SetReserveTimedOut(side, timedOut: false);
    }

    /// <inheritdoc/>
    public bool IsMissingReserveSideAccepted(BattleSideEnum side)
    {
        lock (sync)
        {
            if (side == BattleSideEnum.Defender) return defenderReserveTimedOut;
            if (side == BattleSideEnum.Attacker) return attackerReserveTimedOut;
            return false;
        }
    }

    // Records whether a side's reserve timed out; sides other than attacker and defender are ignored.
    private void SetReserveTimedOut(BattleSideEnum side, bool timedOut)
    {
        lock (sync)
        {
            if (side == BattleSideEnum.Defender) defenderReserveTimedOut = timedOut;
            else if (side == BattleSideEnum.Attacker) attackerReserveTimedOut = timedOut;
        }
    }

    // Forgets both sides' reserve timeouts; callers hold the lock.
    private void ClearReserveTimeouts()
    {
        defenderReserveTimedOut = false;
        attackerReserveTimedOut = false;
    }
}
