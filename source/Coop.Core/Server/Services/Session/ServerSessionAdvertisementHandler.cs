using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Common.Session.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session.Messages;
using Coop.Steam;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Starts the server's tunnel and configured Steam lobby after both network binding and anonymous
/// Steam logon complete, with bounded retries for transient startup failures. Discovery visibility
/// changes who sees the lobby, not whether the server participates in Steam networking.
/// </summary>
public class ServerSessionAdvertisementHandler : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSessionAdvertisementHandler>();
    private const int MaxStartRetries = 3;
    private static readonly TimeSpan StartRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ISessionAdvertiser advertiser;
    private readonly ISteamLobbyOwner lobbyOwner;
    private readonly ISessionTunnelHost tunnelHost;
    private readonly ISessionJoinInfoSource joinInfoSource;
    private readonly INetworkConfig networkConfig;

    private bool listening;
    private bool starting;
    private bool advertised;
    private bool disposed;
    private int connectedPlayers;
    private int startRetryCount;
    private Timer retryTimer;

    public ServerSessionAdvertisementHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ISessionAdvertiser advertiser,
        ISteamLobbyOwner lobbyOwner,
        ISessionTunnelHost tunnelHost,
        ISessionJoinInfoSource joinInfoSource,
        INetworkConfig networkConfig)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.advertiser = advertiser;
        this.lobbyOwner = lobbyOwner;
        this.tunnelHost = tunnelHost;
        this.joinInfoSource = joinInfoSource;
        this.networkConfig = networkConfig;

        messageBroker.Subscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Subscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        lobbyOwner.LobbyChanged += Handle_LobbyChanged;
        SteamGameServerBoot.LoggedOn += OnGameServerLoggedOn;
    }

    private void Handle_ServerListening(MessagePayload<ServerListening> _)
    {
        listening = true;
        TryStartAdvertising();
    }

    private void OnGameServerLoggedOn() => TryStartAdvertising();

    private void Handle_ConnectedPlayersChanged(MessagePayload<ConnectedPlayersChanged> payload)
    {
        int count = Math.Max(0, payload.What.ConnectedPlayers);
        GameThread.RunSafe(() => RefreshConnectedPlayers(count), context: "ServerRefreshConnectedPlayers");
    }

    private void Handle_PlayerConnected(MessagePayload<PlayerConnected> payload)
    {
        if (lobbyOwner.LobbyId == 0) return;

        network.Send(payload.What.PlayerPeer, new NetworkSessionLobbyChanged(lobbyOwner.LobbyId));
    }

    private void Handle_LobbyChanged(ulong lobbyId)
    {
        if (lobbyId == 0) return;

        network.SendAll(new NetworkSessionLobbyChanged(lobbyId));
    }

    private void TryStartAdvertising()
    {
        if (disposed || advertised || starting || !listening || !SteamGameServerBoot.IsLoggedOn) return;
        starting = true;

        GameThread.RunSafe(StartAdvertising, context: "ServerAdvertiseSession");
    }

    private void StartAdvertising()
    {
        try
        {
            tunnelHost.Start(networkConfig.Port);
            advertiser.Advertise(GetCurrentJoinInfo());
            advertised = true;
            starting = false;
            CancelRetry();
        }
        catch (Exception ex)
        {
            starting = false;
            Logger.Error(ex, "Could not start the standalone Steam advertisement");
            ScheduleRetry();
        }
    }

    private void RefreshConnectedPlayers(int count)
    {
        connectedPlayers = count;
        // This flag is set as soon as the first Advertise call returns, including while lobby
        // creation is in flight. Calling again then replaces the advertiser's pending metadata.
        if (disposed || !advertised) return;

        advertiser.Advertise(GetCurrentJoinInfo());
    }

    private SessionJoinInfo GetCurrentJoinInfo()
    {
        var info = joinInfoSource.Get();
        info.ConnectedPlayers = connectedPlayers;
        return info;
    }

    private void ScheduleRetry()
    {
        if (disposed || startRetryCount >= MaxStartRetries) return;

        startRetryCount++;
        CancelRetry();
        retryTimer = new Timer(_ => GameThread.RunSafe(TryStartAdvertising,
            context: "RetryServerSteamAdvertisement"), null, StartRetryDelay, Timeout.InfiniteTimeSpan);
    }

    private void CancelRetry()
    {
        retryTimer?.Dispose();
        retryTimer = null;
    }

    public void Dispose()
    {
        disposed = true;
        CancelRetry();
        messageBroker.Unsubscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Unsubscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Unsubscribe<PlayerConnected>(Handle_PlayerConnected);
        lobbyOwner.LobbyChanged -= Handle_LobbyChanged;
        SteamGameServerBoot.LoggedOn -= OnGameServerLoggedOn;

        GameThread.RunSafe(() =>
        {
            advertiser.StopAdvertising();
            tunnelHost.Stop();
        }, context: "ServerStopAdvertising");
    }
}
