using Common.Messaging;

namespace Common.Network.Session.Messages;

/// <summary>
/// Requests joining a session advertised through a specific Steam lobby.
/// </summary>
public record JoinSteamLobby : ICommand
{
    public ulong LobbyId { get; }

    public JoinSteamLobby(ulong lobbyId)
    {
        LobbyId = lobbyId;
    }
}
