using System;
using System.Collections.Generic;

namespace Common.Network.Session;

/// <summary>Lists public standalone-server lobbies without exposing Steamworks types.</summary>
public interface ISteamLobbyBrowser
{
    /// <param name="onProgress">Receives every public lobby found so far while the search is still running.</param>
    void RequestLobbies(
        Action<IReadOnlyList<SteamLobbySummary>, string> onCompleted,
        Action<IReadOnlyList<SteamLobbySummary>> onProgress = null);
}
