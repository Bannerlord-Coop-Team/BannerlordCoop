using Common;
using Common.Network.Session;
using System;
using System.Threading;

namespace Coop.Steam;

/// <summary>
/// Publishes a standalone-server lobby from the server process's user Steam session while carrying
/// the separate game-server identity as its connect target. The configured Steam lobby type controls
/// whether the session is publicly browsable, restricted to friends, or unlisted by our browser.
/// </summary>
public class SteamPublicLobbyAdvertiser : SteamLobbyAdvertiser
{
    private const int MaxCreateRetries = 3;
    private static readonly TimeSpan CreateRetryDelay = TimeSpan.FromSeconds(5);

    private readonly ISteamPublicLobbyApi publicLobbyApi;
    private readonly ServerVisibility visibility;
    private Timer retryTimer;
    private SessionJoinInfo retryInfo;
    private int retryCount;
    private bool standaloneDisposed;

    public SteamPublicLobbyAdvertiser(ISteamPublicLobbyApi lobbyApi)
        : this(lobbyApi, ServerVisibility.Public)
    {
    }

    public SteamPublicLobbyAdvertiser(ISteamPublicLobbyApi lobbyApi, ServerVisibility visibility)
        : base(lobbyApi)
    {
        if (!Enum.IsDefined(typeof(ServerVisibility), visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        publicLobbyApi = lobbyApi;
        this.visibility = visibility;
    }

    protected override void RequestLobby(int maxMembers, Action<ulong, bool> onCompleted)
    {
        switch (visibility)
        {
            case ServerVisibility.Public:
                publicLobbyApi.CreatePublicLobby(maxMembers, onCompleted);
                return;
            case ServerVisibility.FriendsOnly:
                publicLobbyApi.CreateFriendsOnlyLobby(maxMembers, onCompleted);
                return;
            case ServerVisibility.None:
                // None suppresses only our discovery UI. Keep a normal, fully joinable Steam
                // lobby so the server's tunnel, rich presence, and explicit Steam joins work.
                publicLobbyApi.CreatePublicLobby(maxMembers, onCompleted);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(visibility));
        }
    }

    public override void Advertise(SessionJoinInfo info)
    {
        info.Discoverable = visibility != ServerVisibility.None;
        CancelRetry();
        retryCount = 0;
        retryInfo = null;
        base.Advertise(info);
    }

    protected override bool ApplyAdditionalLobbyData(ulong targetLobbyId)
        => lobbyApi.SetLobbyData(
            targetLobbyId, LobbyDataCodec.VisibilityKey, LobbyDataCodec.EncodeVisibility(visibility));

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
