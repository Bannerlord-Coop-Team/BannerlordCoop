namespace Missions.Naval;

/// <summary>[Game thread] Whether a coop naval battle is running, so the agent-wide station patches stay idle elsewhere.</summary>
internal static class CoopNavalMissionScope
{
    public static bool IsActive { get; set; }
}
