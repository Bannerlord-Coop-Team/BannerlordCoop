namespace Common.Network.Session;

/// <summary>
/// Process-wide discovery capabilities, probed once at mod load.
/// </summary>
public static class SessionDiscovery
{
    public static bool SteamAvailable { get; set; }

    public static ISteamLobbyBrowser SteamLobbyBrowser { get; set; } = UnavailableSteamLobbyBrowser.Instance;

    /// <summary>Empty browser used when Steam is unavailable.</summary>
    private sealed class UnavailableSteamLobbyBrowser : ISteamLobbyBrowser
    {
        public static readonly UnavailableSteamLobbyBrowser Instance = new UnavailableSteamLobbyBrowser();

        public void RequestLobbies(System.Action<System.Collections.Generic.IReadOnlyList<SteamLobbySummary>, string> onCompleted)
        {
            onCompleted(System.Array.Empty<SteamLobbySummary>(), "Steam is unavailable");
        }
    }
}
