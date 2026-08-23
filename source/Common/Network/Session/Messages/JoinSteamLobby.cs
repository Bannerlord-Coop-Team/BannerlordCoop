using Common.Messaging;

namespace Common.Network.Session.Messages;

/// <summary>
/// Requests joining a session advertised through a specific Steam lobby.
/// </summary>
public record JoinSteamLobby : ICommand
{
    public ulong LobbyId { get; }

    /// <summary>
    /// Password to use if the lobby requires one.  When non-null, the join flow skips
    /// the interactive password prompt and supplies this value directly.
    /// </summary>
    public string PreSuppliedPassword { get; }

    public JoinSteamLobby(ulong lobbyId, string preSuppliedPassword = null)
    {
        LobbyId = lobbyId;
        PreSuppliedPassword = preSuppliedPassword;
    }
}
