namespace Missions.Naval;

/// <summary>
/// Whether a coop naval battle is running, so the agent-wide patches stay idle elsewhere. Written on the game thread, read there
/// and on the engine's async agent tick and fixed tick, so it is volatile.
/// </summary>
internal static class CoopNavalMissionScope
{
    private static volatile bool isActive;

    public static bool IsActive
    {
        get => isActive;
        set => isActive = value;
    }
}
