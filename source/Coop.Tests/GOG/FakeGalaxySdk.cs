using Common.Network.Session;
using Coop.GOG;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Tests.GOG;

internal sealed class FakeGalaxySdk : IGalaxySdk
{
    internal readonly struct SentPacket
    {
        public SentPacket(
            ulong remoteUserId,
            byte channel,
            byte[] data,
            GalaxyP2PSendMode sendMode)
        {
            RemoteUserId = remoteUserId;
            Channel = channel;
            Data = data;
            SendMode = sendMode;
        }

        public ulong RemoteUserId { get; }
        public byte Channel { get; }
        public byte[] Data { get; }
        public GalaxyP2PSendMode SendMode { get; }
    }

    private readonly Dictionary<ulong, Dictionary<string, string>> lobbyData =
        new Dictionary<ulong, Dictionary<string, string>>();
    private readonly Dictionary<ulong, ulong> lobbyOwners = new Dictionary<ulong, ulong>();
    private readonly Dictionary<ulong, bool> lobbyDataResults = new Dictionary<ulong, bool>();
    private readonly Queue<bool> sendResults = new Queue<bool>();

    public ulong LocalUserId { get; set; } = 100;
    public string LocalPersonaName { get; set; } = "Galaxy Player";
    public uint UtcNowSeconds { get; set; } = 1_000;
    public ulong NextLobbyId { get; set; } = 42;
    public bool CreateSucceeds { get; set; } = true;
    public bool JoinSucceeds { get; set; } = true;
    public bool LobbyListSucceeds { get; set; } = true;
    public bool CompleteCreateImmediately { get; set; } = true;
    public bool CompleteJoinImmediately { get; set; } = true;
    public bool CompleteLobbyListImmediately { get; set; } = true;
    public bool CompleteLobbyDataImmediately { get; set; } = true;
    public bool CompleteLobbyDataWritesImmediately { get; set; } = true;
    public bool InviteDialogResult { get; set; } = true;
    public bool RichPresenceResult { get; set; } = true;
    public bool AuthenticationSucceeds { get; set; } = true;
    public bool CompleteAuthenticationImmediately { get; set; } = true;
    public ulong AuthenticatedUserId { get; set; } = 100;
    public bool ThrowOnCreate { get; set; }
    public string FailedLobbyDataKey { get; set; } = string.Empty;
    public string ThrowOnLobbyDataWriteKey { get; set; } = string.Empty;
    public ulong ThrowOnLobbyDataRead { get; set; }
    public IReadOnlyList<ulong> LobbyList { get; set; } = Array.Empty<ulong>();
    public List<ulong> LeftLobbies { get; } = new List<ulong>();
    public List<string> InviteDialogs { get; } = new List<string>();
    public List<string> RichPresenceConnects { get; } = new List<string>();
    public List<SentPacket> SentPackets { get; } = new List<SentPacket>();
    public List<(GalaxyLobbyVisibility Visibility, int MaxMembers)> CreateRequests { get; } =
        new List<(GalaxyLobbyVisibility, int)>();
    public List<ulong> LobbyDataRequests { get; } = new List<ulong>();
    public List<ulong> JoinRequests { get; } = new List<ulong>();
    public int AuthenticationRequests { get; private set; }
    public bool RichPresenceCleared { get; private set; }
    public bool Disposed { get; private set; }

    private Action<ulong, bool> pendingCreate;
    private Action<IReadOnlyList<ulong>, bool> pendingLobbyList;
    private readonly Queue<(ulong LobbyId, Action<bool> Callback)> pendingLobbyData =
        new Queue<(ulong, Action<bool>)>();
    private readonly Queue<(ulong LobbyId, string Key, string Value, Action<bool> Callback)>
        pendingLobbyDataWrites =
            new Queue<(ulong, string, string, Action<bool>)>();
    private Action<ulong, bool> pendingJoin;
    private ulong pendingJoinLobbyId;
    private Action<bool> pendingAuthentication;

    public event Action<string> GameJoinRequested;
    public event Action<ulong, byte, byte[]> PacketReceived;

    public void EnsureAuthenticated(Action<bool> onCompleted)
    {
        if (LocalUserId != 0)
        {
            onCompleted(true);
            return;
        }

        AuthenticationRequests++;
        if (CompleteAuthenticationImmediately)
        {
            if (AuthenticationSucceeds) LocalUserId = AuthenticatedUserId;
            onCompleted(AuthenticationSucceeds);
        }
        else
        {
            pendingAuthentication += onCompleted;
        }
    }

    public void CompleteAuthentication(bool success)
    {
        if (success) LocalUserId = AuthenticatedUserId;
        var callback = pendingAuthentication;
        pendingAuthentication = null;
        callback?.Invoke(success);
    }

    public void CreateLobby(
        GalaxyLobbyVisibility visibility,
        int maxMembers,
        Action<ulong, bool> onCompleted)
    {
        CreateRequests.Add((visibility, maxMembers));
        if (ThrowOnCreate) throw new InvalidOperationException("scripted lobby create failure");
        if (CompleteCreateImmediately)
            onCompleted(NextLobbyId, CreateSucceeds);
        else
            pendingCreate = onCompleted;
    }

    public void CompleteCreate()
    {
        Action<ulong, bool> completion = pendingCreate;
        pendingCreate = null;
        completion?.Invoke(NextLobbyId, CreateSucceeds);
    }

    public void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted)
    {
        if (CompleteLobbyListImmediately)
            onCompleted(LobbyList, LobbyListSucceeds);
        else
            pendingLobbyList = onCompleted;
    }

    public void CompleteLobbyList()
    {
        Action<IReadOnlyList<ulong>, bool> completion = pendingLobbyList;
        pendingLobbyList = null;
        completion?.Invoke(LobbyList, LobbyListSucceeds);
    }

    public void RequestLobbyData(ulong lobbyId, Action<bool> onCompleted)
    {
        LobbyDataRequests.Add(lobbyId);
        bool result = !lobbyDataResults.TryGetValue(lobbyId, out bool configured) || configured;
        if (CompleteLobbyDataImmediately)
            onCompleted(result);
        else
            pendingLobbyData.Enqueue((lobbyId, onCompleted));
    }

    public void CompleteAllLobbyData()
    {
        while (pendingLobbyData.Count > 0)
        {
            var pending = pendingLobbyData.Dequeue();
            bool result = !lobbyDataResults.TryGetValue(pending.LobbyId, out bool configured) || configured;
            pending.Callback(result);
        }
    }

    public void SetLobbyDataResult(ulong lobbyId, bool success) =>
        lobbyDataResults[lobbyId] = success;

    public void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted)
    {
        JoinRequests.Add(lobbyId);
        if (CompleteJoinImmediately)
            onCompleted(lobbyId, JoinSucceeds);
        else
        {
            pendingJoinLobbyId = lobbyId;
            pendingJoin = onCompleted;
        }
    }

    public void CompleteJoin()
    {
        Action<ulong, bool> completion = pendingJoin;
        ulong lobbyId = pendingJoinLobbyId;
        pendingJoin = null;
        pendingJoinLobbyId = 0;
        completion?.Invoke(lobbyId, JoinSucceeds);
    }

    public void LeaveLobby(ulong lobbyId) => LeftLobbies.Add(lobbyId);

    public bool SetLobbyData(ulong lobbyId, string key, string value)
    {
        if (string.Equals(key, ThrowOnLobbyDataWriteKey, StringComparison.Ordinal))
            throw new InvalidOperationException("scripted lobby write failure");
        if (string.Equals(key, FailedLobbyDataKey, StringComparison.Ordinal)) return false;
        if (!lobbyData.TryGetValue(lobbyId, out var data))
        {
            data = new Dictionary<string, string>();
            lobbyData.Add(lobbyId, data);
        }
        data[key] = value ?? string.Empty;
        return true;
    }

    public void SetLobbyData(
        ulong lobbyId,
        string key,
        string value,
        Action<bool> onCompleted)
    {
        if (string.Equals(key, ThrowOnLobbyDataWriteKey, StringComparison.Ordinal))
            throw new InvalidOperationException("scripted lobby write failure");

        if (CompleteLobbyDataWritesImmediately)
        {
            onCompleted(SetLobbyData(lobbyId, key, value));
            return;
        }

        pendingLobbyDataWrites.Enqueue((lobbyId, key, value, onCompleted));
    }

    public void CompleteAllLobbyDataWrites()
    {
        while (pendingLobbyDataWrites.Count > 0)
        {
            var pending = pendingLobbyDataWrites.Dequeue();
            pending.Callback(SetLobbyData(pending.LobbyId, pending.Key, pending.Value));
        }
    }

    public string GetLobbyData(ulong lobbyId, string key)
    {
        if (ThrowOnLobbyDataRead == lobbyId) throw new InvalidOperationException("scripted lobby read failure");
        return lobbyData.TryGetValue(lobbyId, out var data) && data.TryGetValue(key, out string value)
            ? value
            : string.Empty;
    }

    public void SetLobbyOwner(ulong lobbyId, ulong ownerId) => lobbyOwners[lobbyId] = ownerId;

    public ulong GetLobbyOwner(ulong lobbyId) =>
        lobbyOwners.TryGetValue(lobbyId, out ulong ownerId) ? ownerId : LocalUserId;

    public bool ShowInviteDialog(string connectionString)
    {
        InviteDialogs.Add(connectionString);
        return InviteDialogResult;
    }

    public bool SetRichPresenceConnect(string connectionString)
    {
        RichPresenceConnects.Add(connectionString);
        return RichPresenceResult;
    }

    public void ClearRichPresenceConnect() => RichPresenceCleared = true;

    public bool SendP2P(
        ulong remoteUserId,
        byte channel,
        byte[] data,
        GalaxyP2PSendMode sendMode)
    {
        SentPackets.Add(new SentPacket(remoteUserId, channel, data.ToArray(), sendMode));
        return sendResults.Count == 0 || sendResults.Dequeue();
    }

    public void EnqueueSendResult(bool result) => sendResults.Enqueue(result);
    public string GetConnectionType(ulong remoteUserId) => "fake-galaxy";

    public void RaiseGameJoinRequested(string connectionString) =>
        GameJoinRequested?.Invoke(connectionString);

    public void RaisePacket(ulong remoteUserId, byte channel, byte[] packet) =>
        PacketReceived?.Invoke(remoteUserId, channel, packet);

    public void Dispose() => Disposed = true;
}
