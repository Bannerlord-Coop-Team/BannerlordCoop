using System;
using System.Collections.Generic;

namespace Common.Network.Session;

/// <summary>Lists public standalone-server lobbies without exposing Steamworks types.</summary>
public interface ISteamLobbyBrowser
{
    void RequestLobbies(Action<IReadOnlyList<SteamLobbySummary>, string> onCompleted);
}
