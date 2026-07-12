using Common;
using Common.Network.Session;
using System;
using System.Threading;

namespace Coop.Steam;

/// <summary>
/// Publishes a browsable lobby from the server process's user Steam session while carrying the
/// separate game-server identity as its connect target.
/// </summary>
public class SteamPublicLobbyAdvertiser : SteamLobbyAdvertiser
{
    private const int MaxCreateRetries = 3;
    private static readonly TimeSpan CreateRetryDelay = TimeSpan.FromSeconds(5);

    private readonly ISteamPublicLobbyApi publicLobbyApi;
    private Timer retryTimer;
    private SessionJoinInfo retryInfo;
    private int retryCount;
    private bool standaloneDisposed;

    public SteamPublicLobbyAdvertiser(ISteamPublicLobbyApi lobbyApi) : base(lobbyApi)
    {
        publicLobbyApi = lobbyApi;
    }

    protected override void RequestLobby(int maxMembers, Action<ulong, bool> onCompleted)
        => publicLobbyApi.CreatePublicLobby(maxMembers, onCompleted);

    public override void Advertise(SessionJoinInfo info)
    {
        CancelRetry();
        retryCount = 0;
        retryInfo = null;
        base.Advertise(info);
    }

    protected override void OnLobbyUnavailable(SessionJoinInfo info)
    {
        if (standaloneDisposed || info == null || retryCount >= MaxCreateRetries) return;

        retryCount++;
        retryInfo = info;
        CancelRetry();
        retryTimer = new Timer(_ => GameThread.RunSafe(RetryCreate,
            context: "RetryPublicSteamLobby"), null, CreateRetryDelay, Timeout.InfiniteTimeSpan);
    }

    private void RetryCreate()
    {
        CancelRetry();
        if (standaloneDisposed || retryInfo == null) return;

        base.Advertise(retryInfo);
    }

    public override void StopAdvertising()
    {
        retryInfo = null;
        retryCount = 0;
        CancelRetry();
        base.StopAdvertising();
    }

    public override void Dispose()
    {
        if (standaloneDisposed) return;
        standaloneDisposed = true;
        retryInfo = null;
        CancelRetry();
        base.Dispose();
    }

    private void CancelRetry()
    {
        retryTimer?.Dispose();
        retryTimer = null;
    }
}
