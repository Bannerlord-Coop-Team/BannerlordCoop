using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Common.Session.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session.Messages;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Starts the server's tunnel and provider listing after both network binding and provider
/// readiness, with bounded retries for transient startup failures.
/// </summary>
public class ServerSessionAdvertisementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSessionAdvertisementHandler>();
    private const int MaxStartRetries = 3;
    private static readonly TimeSpan StartRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ISessionAdvertiser advertiser;
    private readonly ISessionAdvertisementOwner advertisementOwner;
    private readonly ISessionServerReadiness serverReadiness;
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
        ISessionAdvertisementOwner advertisementOwner,
        ISessionServerReadiness serverReadiness,
        ISessionTunnelHost tunnelHost,
        ISessionJoinInfoSource joinInfoSource,
        INetworkConfig networkConfig)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.advertiser = advertiser;
        this.advertisementOwner = advertisementOwner;
        this.serverReadiness = serverReadiness;
        this.tunnelHost = tunnelHost;
        this.joinInfoSource = joinInfoSource;
        this.networkConfig = networkConfig;

        messageBroker.Subscribe<ServerListening>(Handle_ServerListening);
        messageBroker.Subscribe<ConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        advertisementOwner.ListingChanged += Handle_ListingChanged;
        serverReadiness.Ready += Handle_ServerReady;
    }

    private void Handle_ServerListening(MessagePayload<ServerListening> _)
    {
        listening = true;
        TryStartAdvertising();
    }

    private void Handle_ServerReady() => TryStartAdvertising();

    private void Handle_ConnectedPlayersChanged(MessagePayload<ConnectedPlayersChanged> payload)
    {
        int count = Math.Max(0, payload.What.ConnectedPlayers);
        GameThread.RunSafe(() => RefreshConnectedPlayers(count), context: "ServerRefreshConnectedPlayers");
    }

    private void Handle_PlayerConnected(MessagePayload<PlayerConnected> payload)
    {
        if (!advertisementOwner.ListingId.IsValid) return;

        network.Send(payload.What.PlayerPeer, new NetworkSessionLobbyChanged(advertisementOwner.ListingId));
    }

    private void Handle_ListingChanged(SessionListingId listingId)
    {
        if (!listingId.IsValid) return;

        network.SendAll(new NetworkSessionLobbyChanged(listingId));
    }

    private void TryStartAdvertising()
    {
        if (disposed || advertised || starting || !listening || !serverReadiness.IsReady) return;
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
            Logger.Error(ex, "Could not start the standalone provider advertisement");
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
            context: "RetryServerProviderAdvertisement"), null, StartRetryDelay, Timeout.InfiniteTimeSpan);
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
        advertisementOwner.ListingChanged -= Handle_ListingChanged;
        serverReadiness.Ready -= Handle_ServerReady;

        GameThread.RunSafe(() =>
        {
            advertiser.StopAdvertising();
            tunnelHost.Stop();
        }, context: "ServerStopAdvertising");
    }
}
