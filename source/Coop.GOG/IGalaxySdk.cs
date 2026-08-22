using System;
using System.Collections.Generic;

namespace Coop.GOG;

internal enum GalaxyLobbyVisibility
{
    Private,
    FriendsOnly,
    Public,
}

internal enum GalaxyP2PSendMode
{
    Unreliable,
    Reliable,
}

/// <summary>BCL-only seam over the Galaxy lobby, overlay, and P2P surfaces.</summary>
internal interface IGalaxySdk : IDisposable
{
    ulong LocalUserId { get; }
    string LocalPersonaName { get; }
    uint UtcNowSeconds { get; }

    event Action<string> GameJoinRequested;
    event Action<ulong, byte, byte[]> PacketReceived;

    void EnsureAuthenticated(Action<bool> onCompleted);
    void CreateLobby(
        GalaxyLobbyVisibility visibility,
        int maxMembers,
        Action<ulong, bool> onCompleted);
    void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted);
    void RequestLobbyData(ulong lobbyId, Action<bool> onCompleted);
    void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted);
    void LeaveLobby(ulong lobbyId);
    void SetLobbyData(ulong lobbyId, string key, string value, Action<bool> onCompleted);
    string GetLobbyData(ulong lobbyId, string key);
    ulong GetLobbyOwner(ulong lobbyId);

    bool ShowInviteDialog(string connectionString);
    bool SetRichPresenceConnect(string connectionString);
    void ClearRichPresenceConnect();

    bool SendP2P(
        ulong remoteUserId,
        byte channel,
        byte[] data,
        GalaxyP2PSendMode sendMode);
    string GetConnectionType(ulong remoteUserId);
}
