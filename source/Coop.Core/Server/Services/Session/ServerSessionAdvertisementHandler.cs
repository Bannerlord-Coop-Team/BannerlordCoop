using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Server.Services.Session.Messages;
using Coop.Steam;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Starts the server's tunnel and public lobby after both network binding and anonymous Steam
/// logon complete, with bounded retries for transient startup failures.
/// </summary>
public class ServerSessionAdvertisementHandler : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSessionAdvertisementHandler>();
    private const int MaxStartRetries = 3;
    private static readonly TimeSpan StartRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IMessageBroker messageBroker;
    private readonly ISessionAdvertiser advertiser;
    private readonly ISessionTunnelHost tunnelHost;
    private readonly ISessionJoinInfoSource joinInfoSource;
    private readonly INetworkConfig networkConfig;

    private bool listening;
    private bool starting;
    private bool advertised;
    private bool disposed;
    private int startRetryCount;
    private Timer retryTimer;

    public ServerSessionAdvertisementHandler(
        IMessageBroker messageBroker,
        ISessionAdvertiser advertiser,
        ISessionTunnelHost tunnelHost,
        ISessionJoinInfoSource joinInfoSource,
        INetworkConfig networkConfig)
    {
        this.messageBroker = messageBroker;
        this.advertiser = advertiser;
        this.tunnelHost = tunnelHost;
        this.joinInfoSource = joinInfoSource;
        this.networkConfig = networkConfig;

        messageBroker.Subscribe<ServerListening>(Handle_ServerListening);
        SteamGameServerBoot.LoggedOn += OnGameServerLoggedOn;
    }

    private void Handle_ServerListening(MessagePayload<ServerListening> _)
    {
        listening = true;
        TryStartAdvertising();
    }

    private void OnGameServerLoggedOn() => TryStartAdvertising();

    private void TryStartAdvertising()
    {
        if (disposed || advertised || starting || !listening || !SteamGameServerBoot.IsLoggedOn) return;
        starting = true;

        var info = joinInfoSource.Get();
        GameThread.RunSafe(() => StartAdvertising(info), context: "ServerAdvertiseSession");
    }

    private void StartAdvertising(SessionJoinInfo info)
    {
        try
        {
            tunnelHost.Start(networkConfig.Port);
            advertiser.Advertise(info);
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
        SteamGameServerBoot.LoggedOn -= OnGameServerLoggedOn;

        GameThread.RunSafe(() =>
        {
            advertiser.StopAdvertising();
            tunnelHost.Stop();
        }, context: "ServerStopAdvertising");
    }
}
