namespace GameInterface.Services.MapEvents;

internal static class MapEventConfig
{
    public const bool Enabled = true;
    public const bool Debug = true;

    private static volatile bool allowRaidAiIntervention = true;

    public static bool AllowRaidAiIntervention
    {
        get => allowRaidAiIntervention;
        set => allowRaidAiIntervention = value;
    }

    // How long a battle whose players are all offline waits for one to rejoin before the server resolves it
    public static readonly System.TimeSpan OfflinePlayerGracePeriod = System.TimeSpan.FromMinutes(5);
}
