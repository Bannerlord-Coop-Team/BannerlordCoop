using Common;
using System;
using System.Collections.Generic;

namespace Coop.GOG;

internal sealed class GalaxySdk : IGalaxySdk
{
    public GalaxySdk(bool gameServer) => throw Unavailable();

    public ulong LocalUserId => throw Unavailable();
    public string LocalPersonaName => throw Unavailable();
    public uint UtcNowSeconds => throw Unavailable();

    public event Action<string> GameJoinRequested
    {
        add => throw Unavailable();
        remove => throw Unavailable();
    }

    public event Action<ulong, byte, byte[]> PacketReceived
    {
        add => throw Unavailable();
        remove => throw Unavailable();
    }

    public void CreateLobby(
        GalaxyLobbyVisibility visibility,
        int maxMembers,
        Action<ulong, bool> onCompleted) => throw Unavailable();

    public void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted) => throw Unavailable();
    public void RequestLobbyData(ulong lobbyId, Action<bool> onCompleted) => throw Unavailable();
    public void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted) => throw Unavailable();
    public void LeaveLobby(ulong lobbyId) => throw Unavailable();

    public void SetLobbyData(
        ulong lobbyId,
        string key,
        string value,
        Action<bool> onCompleted) => throw Unavailable();

    public string GetLobbyData(ulong lobbyId, string key) => throw Unavailable();
    public ulong GetLobbyOwner(ulong lobbyId) => throw Unavailable();
    public bool ShowInviteDialog(string connectionString) => throw Unavailable();
    public bool SetRichPresenceConnect(string connectionString) => throw Unavailable();
    public void ClearRichPresenceConnect() => throw Unavailable();

    public bool SendP2P(
        ulong remoteUserId,
        byte channel,
        byte[] data,
        GalaxyP2PSendMode sendMode) => throw Unavailable();

    public string GetConnectionType(ulong remoteUserId) => throw Unavailable();
    public void Dispose() { }

    private static PlatformNotSupportedException Unavailable() =>
        new PlatformNotSupportedException(
            "GalaxyCSharp.dll was unavailable when Coop.GOG was built");
}

internal static class GalaxyGameServerBoot
{
    public static bool HasConfiguredCredentials => false;
    public static bool IsReady => false;

    public static event Action Ready
    {
        add { }
        remove { }
    }

    public static bool TryStart() => false;
    public static void ProcessData() { }
    public static void Shutdown() { }
}

internal sealed class GalaxyGameServerCallbackPump : IUpdateable
{
    public int Priority => UpdatePriority.MainLoop.PlatformCallbacks;
    public void Update(TimeSpan frameTime) { }
}
