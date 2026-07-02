using System;

namespace Coop.Steam;

/// <summary>
/// Thin seam over the Steamworks lobby/friends surface. Signatures deliberately use only
/// BCL types so consumers (and tests) never load the Steamworks assembly.
/// </summary>
public interface ISteamLobbyApi : IDisposable
{
    bool IsOverlayEnabled { get; }

    void CreateFriendsOnlyLobby(int maxMembers, Action<ulong, bool> onCompleted);
    void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted);
    void LeaveLobby(ulong lobbyId);
    bool SetLobbyData(ulong lobbyId, string key, string value);
    string GetLobbyData(ulong lobbyId, string key);
    void OpenInviteDialog(ulong lobbyId);
    bool SetRichPresenceConnect(string value);
    void ClearRichPresenceConnect();
    string GetLaunchCommandLine();

    /// <summary>Fires when the user accepts a lobby invite or clicks Join Game on a lobby.</summary>
    event Action<ulong> LobbyJoinRequested;

    /// <summary>Fires with a connect string from a rich-presence join or new launch parameters.</summary>
    event Action<string> ConnectStringReceived;
}
