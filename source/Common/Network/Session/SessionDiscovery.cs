namespace Common.Network.Session;

/// <summary>
/// Process-wide discovery capabilities, probed once at mod load.
/// </summary>
public static class SessionDiscovery
{
    public static bool SteamAvailable { get; set; }
}
