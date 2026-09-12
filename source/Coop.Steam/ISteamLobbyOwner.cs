using System;

namespace Coop.Steam;

/// <summary>
/// Exposes the Steam lobby owned by the session advertiser.
/// </summary>
public interface ISteamLobbyOwner
{
    ulong LobbyId { get; }
    event Action<ulong> LobbyChanged;
}
