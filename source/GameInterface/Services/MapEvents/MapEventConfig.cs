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
}
