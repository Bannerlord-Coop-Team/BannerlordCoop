using System;
using System.Collections.Generic;

namespace Coop.Steam;

/// <summary>
/// Thin seam over the Steamworks lobby/friends surface. Signatures deliberately use only
/// BCL types so consumers (and tests) never load the Steamworks assembly.
/// </summary>
public interface ISteamLobbyApi : IDisposable
{
    bool IsOverlayEnabled { get; }
    /// <summary>Display name for the local Steam user that creates and owns the lobby.</summary>
    string LocalPersonaName { get; }

    void CreateFriendsOnlyLobby(int maxMembers, Action<ulong, bool> onCompleted);
    void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted);
    void LeaveLobby(ulong lobbyId);
    bool SetLobbyData(ulong lobbyId, string key, string value);
    string GetLobbyData(ulong lobbyId, string key);
    /// <summary>Steam id of the lobby's owner; only valid while a member of the lobby.</summary>
    ulong GetLobbyOwner(ulong lobbyId);
    void OpenInviteDialog(ulong lobbyId);
    bool SetRichPresenceConnect(string value);
    void ClearRichPresenceConnect();
    string GetLaunchCommandLine();

    /// <summary>Fires when the user accepts a lobby invite or clicks Join Game on a lobby.</summary>
    event Action<ulong> LobbyJoinRequested;

    /// <summary>Fires with a connect string from a rich-presence join or new launch parameters.</summary>
    event Action<string> ConnectStringReceived;
}

/// <summary>Standalone-server discovery operations layered on the existing invite-lobby API.</summary>
public interface ISteamPublicLobbyApi : ISteamLobbyApi
{
    /// <summary>Creates a browsable public lobby for a standalone server.</summary>
    void CreatePublicLobby(int maxMembers, Action<ulong, bool> onCompleted);

    /// <summary>Gets lobby ids advertised by Steam friends playing this app.</summary>
    IReadOnlyList<ulong> GetFriendLobbyIds();

    /// <summary>Refreshes metadata for a lobby obtained through a friend rather than public search.</summary>
    void RequestLobbyData(ulong lobbyId, Action<bool> onCompleted);

    void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted);
}
