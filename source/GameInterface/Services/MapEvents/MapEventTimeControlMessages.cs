namespace GameInterface.Services.MapEvents;

/// <summary>
/// User-facing notices for the map-event fast-forward lock. Shared by the server
/// (host display) and the client time handler so the wording stays consistent.
/// </summary>
public static class MapEventTimeControlMessages
{
    public const string FastForwardDisabled =
        "A player is in a Map Event. The game can no longer be fast forwarded.";

    public const string FastForwardEnabled =
        "No more players are in map events. The game can now be fast forwarded.";

    public static string FastForwardBlocked(int playersInMapEvent)
        => $"{playersInMapEvent} player(s) are in a map event. The game cannot be fast forwarded";
}
