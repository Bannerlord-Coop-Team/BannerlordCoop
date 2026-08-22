using Common.Network.Session;
using Galaxy.Api;
using System;
using System.Collections.Generic;

namespace Coop.GOG;

/// <summary>Uses Bannerlord's initialized Galaxy client or an explicitly initialized game server.</summary>
internal sealed class GalaxySdk : IGalaxySdk
{
    private const string RichPresenceConnectKey = "connect";

    private readonly bool gameServer;
    private readonly INetworking networking;
    private readonly IMatchmaking matchmaking;
    private readonly IUser user;
    private readonly IFriends friends;
    private readonly IDisposable networkingListener;
    private readonly IDisposable gameJoinListener;
    private readonly List<Action<bool>> authenticationWaiters = new List<Action<bool>>();
    private readonly HashSet<LobbyDataUpdateListener> lobbyDataUpdateListeners =
        new HashSet<LobbyDataUpdateListener>();
    private AuthenticationListener authenticationListener;
    private bool disposed;

    public GalaxySdk(bool gameServer)
    {
        this.gameServer = gameServer;
        networking = gameServer ? GalaxyInstance.GameServerNetworking() : GalaxyInstance.Networking();
        matchmaking = gameServer ? GalaxyInstance.GameServerMatchmaking() : GalaxyInstance.Matchmaking();
        user = gameServer ? GalaxyInstance.GameServerUser() : GalaxyInstance.User();
        friends = gameServer ? null : GalaxyInstance.Friends();

        networkingListener = gameServer
            ? new ServerPacketListener(DrainPackets)
            : new ClientPacketListener(DrainPackets);
        if (!gameServer)
            gameJoinListener = new GameJoinListener(connectionString => GameJoinRequested?.Invoke(connectionString));
    }

    public ulong LocalUserId
    {
        get
        {
            if (!user.SignedIn()) return 0;

            using (GalaxyID galaxyId = user.GetGalaxyID())
            {
                return galaxyId != null &&
                    galaxyId.IsValid() &&
                    galaxyId.GetIDType() == GalaxyID.IDType.ID_TYPE_USER
                        ? galaxyId.ToUint64()
                        : 0;
            }
        }
    }

    public string LocalPersonaName => gameServer ? "Dedicated Server" : friends.GetPersonaName();
    public uint UtcNowSeconds => unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public event Action<string> GameJoinRequested;
    public event Action<ulong, byte, byte[]> PacketReceived;

    public void EnsureAuthenticated(Action<bool> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
        if (disposed || gameServer)
        {
            onCompleted(false);
            return;
        }

        if (LocalUserId != 0)
        {
            onCompleted(true);
            return;
        }

        authenticationWaiters.Add(onCompleted);
        if (authenticationListener != null) return;

        authenticationListener = new AuthenticationListener(CompleteAuthentication);
        try
        {
            user.SignInGalaxy(requireOnline: true, authenticationListener);
        }
        catch
        {
            CompleteAuthentication(false);
        }
    }

    public void CreateLobby(
        GalaxyLobbyVisibility visibility,
        int maxMembers,
        Action<ulong, bool> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));

        var listener = new LobbyCreatedListener(onCompleted);
        matchmaking.CreateLobby(
            gameServer ? LobbyType.LOBBY_TYPE_PUBLIC : ToLobbyType(visibility),
            checked((uint)maxMembers),
            joinable: true,
            gameServer
                ? LobbyTopologyType.LOBBY_TOPOLOGY_TYPE_FCM
                : LobbyTopologyType.LOBBY_TOPOLOGY_TYPE_CONNECTIONLESS,
            listener);
    }

    public void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));

        matchmaking.AddRequestLobbyListStringFilter(
            SessionListingDataCodec.TunnelProviderKey,
            GalaxySessionProvider.ProviderId,
            LobbyComparisonType.LOBBY_COMPARISON_TYPE_EQUAL);
        matchmaking.RequestLobbyList(
            allowFullLobbies: true,
            new LobbyListListener(matchmaking, onCompleted));
    }

    public void RequestLobbyData(ulong lobbyId, Action<bool> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));

        using (var galaxyLobbyId = new GalaxyID(lobbyId))
        {
            matchmaking.RequestLobbyData(
                galaxyLobbyId,
                new LobbyDataListener(onCompleted));
        }
    }

    public void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));

        using (var galaxyLobbyId = new GalaxyID(lobbyId))
        {
            matchmaking.JoinLobby(galaxyLobbyId, new LobbyEnteredListener(onCompleted));
        }
    }

    public void LeaveLobby(ulong lobbyId)
    {
        using (var galaxyLobbyId = new GalaxyID(lobbyId))
        {
            matchmaking.LeaveLobby(galaxyLobbyId);
        }
    }

    public void SetLobbyData(
        ulong lobbyId,
        string key,
        string value,
        Action<bool> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));

        LobbyDataUpdateListener listener = null;
        listener = new LobbyDataUpdateListener(success =>
        {
            lobbyDataUpdateListeners.Remove(listener);
            onCompleted(success);
        });
        lobbyDataUpdateListeners.Add(listener);

        try
        {
            using (var galaxyLobbyId = new GalaxyID(lobbyId))
            {
                matchmaking.SetLobbyData(
                    galaxyLobbyId,
                    key,
                    value ?? string.Empty,
                    listener);
            }
        }
        catch
        {
            lobbyDataUpdateListeners.Remove(listener);
            listener.Dispose();
            throw;
        }
    }

    public string GetLobbyData(ulong lobbyId, string key)
    {
        using (var galaxyLobbyId = new GalaxyID(lobbyId))
        {
            return matchmaking.GetLobbyData(galaxyLobbyId, key);
        }
    }

    public ulong GetLobbyOwner(ulong lobbyId)
    {
        using (var galaxyLobbyId = new GalaxyID(lobbyId))
        using (GalaxyID ownerId = matchmaking.GetLobbyOwner(galaxyLobbyId))
        {
            return ownerId.ToUint64();
        }
    }

    public bool ShowInviteDialog(string connectionString)
    {
        if (gameServer || friends == null) return false;

        friends.ShowOverlayInviteDialog(connectionString);
        return true;
    }

    public bool SetRichPresenceConnect(string connectionString)
    {
        if (gameServer || friends == null) return false;

        friends.SetRichPresence(RichPresenceConnectKey, connectionString);
        return true;
    }

    public void ClearRichPresenceConnect()
    {
        if (!gameServer && friends != null)
            friends.DeleteRichPresence(RichPresenceConnectKey);
    }

    public bool SendP2P(
        ulong remoteUserId,
        byte channel,
        byte[] data,
        GalaxyP2PSendMode sendMode)
    {
        using (var remoteGalaxyId = new GalaxyID(remoteUserId))
        {
            return networking.SendP2PPacket(
                remoteGalaxyId,
                data,
                checked((uint)data.Length),
                sendMode == GalaxyP2PSendMode.Reliable
                    ? P2PSendType.P2P_SEND_RELIABLE_IMMEDIATE
                    : P2PSendType.P2P_SEND_UNRELIABLE_IMMEDIATE,
                channel);
        }
    }

    public string GetConnectionType(ulong remoteUserId)
    {
        using (var remoteGalaxyId = new GalaxyID(remoteUserId))
        {
            return networking.GetConnectionType(remoteGalaxyId).ToString();
        }
    }

    private void DrainPackets(uint messageSize, byte channel)
    {
        if (messageSize == 0) return;

        var data = new byte[messageSize];
        uint readSize = 0;
        using (var sender = new GalaxyID())
        {
            if (!networking.PeekP2PPacket(data, messageSize, ref readSize, ref sender, channel))
                return;

            if (readSize != data.Length)
                Array.Resize(ref data, checked((int)readSize));

            PacketReceived?.Invoke(sender.ToUint64(), channel, data);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        var dataUpdateListeners = new LobbyDataUpdateListener[lobbyDataUpdateListeners.Count];
        lobbyDataUpdateListeners.CopyTo(dataUpdateListeners);
        lobbyDataUpdateListeners.Clear();
        foreach (var listener in dataUpdateListeners) listener.Dispose();

        authenticationListener?.Dispose();
        authenticationListener = null;
        CompleteAuthenticationWaiters(false);

        networkingListener?.Dispose();
        gameJoinListener?.Dispose();
    }

    private void CompleteAuthentication(bool success)
    {
        authenticationListener?.Dispose();
        authenticationListener = null;

        bool authenticated = false;
        if (success)
        {
            try
            {
                authenticated = LocalUserId != 0;
            }
            catch
            {
            }
        }

        CompleteAuthenticationWaiters(authenticated);
    }

    private void CompleteAuthenticationWaiters(bool success)
    {
        Action<bool>[] waiters = authenticationWaiters.ToArray();
        authenticationWaiters.Clear();
        foreach (Action<bool> waiter in waiters) waiter(success);
    }

    private static LobbyType ToLobbyType(GalaxyLobbyVisibility visibility) => visibility switch
    {
        GalaxyLobbyVisibility.Private => LobbyType.LOBBY_TYPE_PRIVATE,
        GalaxyLobbyVisibility.FriendsOnly => LobbyType.LOBBY_TYPE_FRIENDS_ONLY,
        GalaxyLobbyVisibility.Public => LobbyType.LOBBY_TYPE_PUBLIC,
        _ => throw new ArgumentOutOfRangeException(nameof(visibility)),
    };

    private sealed class AuthenticationListener : IAuthListener
    {
        private readonly Action<bool> completed;

        public AuthenticationListener(Action<bool> completed)
        {
            this.completed = completed;
        }

        public override void OnAuthSuccess() => completed(true);
        public override void OnAuthFailure(FailureReason failureReason) => completed(false);
        public override void OnAuthLost() => completed(false);
    }

    private sealed class LobbyCreatedListener : ILobbyCreatedListener
    {
        private readonly Action<ulong, bool> completed;

        public LobbyCreatedListener(Action<ulong, bool> completed)
        {
            this.completed = completed;
        }

        public override void OnLobbyCreated(GalaxyID lobbyID, LobbyCreateResult result)
        {
            try
            {
                completed(lobbyID.ToUint64(), result == LobbyCreateResult.LOBBY_CREATE_RESULT_SUCCESS);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class LobbyEnteredListener : ILobbyEnteredListener
    {
        private readonly Action<ulong, bool> completed;

        public LobbyEnteredListener(Action<ulong, bool> completed)
        {
            this.completed = completed;
        }

        public override void OnLobbyEntered(GalaxyID lobbyID, LobbyEnterResult result)
        {
            try
            {
                completed(lobbyID.ToUint64(), result == LobbyEnterResult.LOBBY_ENTER_RESULT_SUCCESS);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class LobbyListListener : ILobbyListListener
    {
        private readonly IMatchmaking matchmaking;
        private readonly Action<IReadOnlyList<ulong>, bool> completed;

        public LobbyListListener(
            IMatchmaking matchmaking,
            Action<IReadOnlyList<ulong>, bool> completed)
        {
            this.matchmaking = matchmaking;
            this.completed = completed;
        }

        public override void OnLobbyList(uint lobbyCount, LobbyListResult result)
        {
            try
            {
                if (result != LobbyListResult.LOBBY_LIST_RESULT_SUCCESS)
                {
                    completed(Array.Empty<ulong>(), false);
                    return;
                }

                var lobbyIds = new List<ulong>(checked((int)lobbyCount));
                for (uint index = 0; index < lobbyCount; index++)
                {
                    using (GalaxyID galaxyLobbyId = matchmaking.GetLobbyByIndex(index))
                    {
                        ulong lobbyId = galaxyLobbyId.ToUint64();
                        if (lobbyId != 0) lobbyIds.Add(lobbyId);
                    }
                }
                completed(lobbyIds, true);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class LobbyDataListener : ILobbyDataRetrieveListener
    {
        private readonly Action<bool> completed;

        public LobbyDataListener(Action<bool> completed)
        {
            this.completed = completed;
        }

        public override void OnLobbyDataRetrieveSuccess(GalaxyID lobbyID)
        {
            try
            {
                completed(true);
            }
            finally
            {
                Dispose();
            }
        }

        public override void OnLobbyDataRetrieveFailure(GalaxyID lobbyID, FailureReason failureReason)
        {
            try
            {
                completed(false);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class LobbyDataUpdateListener : ILobbyDataUpdateListener
    {
        private Action<bool> completed;

        public LobbyDataUpdateListener(Action<bool> completed)
        {
            this.completed = completed;
        }

        public override void OnLobbyDataUpdateSuccess(GalaxyID lobbyID) => Complete(success: true);

        public override void OnLobbyDataUpdateFailure(GalaxyID lobbyID, FailureReason failureReason) =>
            Complete(success: false);

        private void Complete(bool success)
        {
            var callback = completed;
            if (callback == null) return;
            completed = null;

            try
            {
                callback(success);
            }
            finally
            {
                Dispose();
            }
        }
    }

    private sealed class GameJoinListener : GlobalGameJoinRequestedListener
    {
        private readonly Action<string> requested;

        public GameJoinListener(Action<string> requested)
        {
            this.requested = requested;
        }

        public override void OnGameJoinRequested(GalaxyID userID, string connectionString) =>
            requested(connectionString);
    }

    private sealed class ClientPacketListener : GlobalNetworkingListener
    {
        private readonly Action<uint, byte> packetAvailable;

        public ClientPacketListener(Action<uint, byte> packetAvailable)
        {
            this.packetAvailable = packetAvailable;
        }

        public override void OnP2PPacketAvailable(uint msgSize, byte channel) =>
            packetAvailable(msgSize, channel);
    }

    private sealed class ServerPacketListener : GameServerGlobalNetworkingListener
    {
        private readonly Action<uint, byte> packetAvailable;

        public ServerPacketListener(Action<uint, byte> packetAvailable)
        {
            this.packetAvailable = packetAvailable;
        }

        public override void OnP2PPacketAvailable(uint msgSize, byte channel) =>
            packetAvailable(msgSize, channel);
    }
}
