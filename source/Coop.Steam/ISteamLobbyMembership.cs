namespace Coop.Steam;

/// <summary>
/// Tracks the Steam lobby joined by the local player for the current co-op session.
/// </summary>
public interface ISteamLobbyMembership
{
    ulong LobbyId { get; }
    bool IsInLobby { get; }

    void JoinSessionLobby(ulong lobbyId);
    void LeaveSessionLobby();
}
