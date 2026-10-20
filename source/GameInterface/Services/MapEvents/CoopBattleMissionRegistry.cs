namespace GameInterface.Services.MapEvents;

/// <summary>
/// Tracks which coop battle windows are open on this machine, so GameInterface patches can tell a coop battle
/// mission apart from a mission-less battle. Each window is keyed by the battle's map-event id.
/// </summary>
public interface ICoopBattleMissionRegistry
{
    /// <summary>
    /// True from the moment a coop battle mission finishes loading until its controller is disposed. Coop
    /// mission victories then report through NetworkBattleResultReady instead of the legacy state relay.
    /// </summary>
    bool IsMissionActive { get; }

    /// <summary>Opens the loaded-mission window for <paramref name="mapEventId"/>, replacing any earlier one.</summary>
    void BeginMission(string mapEventId);

    /// <summary>Closes the loaded-mission window only if <paramref name="mapEventId"/> still owns it.</summary>
    void EndMission(string mapEventId);
}

/// <inheritdoc cref="ICoopBattleMissionRegistry"/>
public class CoopBattleMissionRegistry : ICoopBattleMissionRegistry
{
    private readonly object sync = new();
    private bool isMissionActive;
    private string missionId;

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

    public void BeginMission(string mapEventId)
    {
        lock (sync)
        {
            isMissionActive = true;
            missionId = mapEventId;
        }
    }

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
}
