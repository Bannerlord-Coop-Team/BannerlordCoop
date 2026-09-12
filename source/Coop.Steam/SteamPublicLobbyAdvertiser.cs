using Common;
using Common.Logging;
using Common.Network.Session;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<SteamPublicLobbyAdvertiser>();
    private const int MaxCreateRetries = 3;
    private const int MaxConsecutiveRenewalFailures = 3;
    internal const uint AdvertisementLeaseSeconds = 60;
    private static readonly TimeSpan CreateRetryDelay = TimeSpan.FromSeconds(5);

    private readonly ISteamPublicLobbyApi publicLobbyApi;
    private readonly ISteamLobbyLeaseRenewer leaseRenewer;
    private readonly ServerVisibility visibility;
    private Timer retryTimer;
    private SessionJoinInfo activeInfo;
    private SessionJoinInfo retryInfo;
    private int retryCount;
    private int consecutiveRenewalFailures;
    private bool leasePublished;
    private bool standaloneDisposed;

    public SteamPublicLobbyAdvertiser(ISteamPublicLobbyApi lobbyApi)
        : this(lobbyApi, ServerVisibility.Public, new SteamLobbyLeaseRenewer())
    {
    }

    public SteamPublicLobbyAdvertiser(ISteamPublicLobbyApi lobbyApi, ServerVisibility visibility)
        : this(lobbyApi, visibility, new SteamLobbyLeaseRenewer())
    {
    }

    public SteamPublicLobbyAdvertiser(
        ISteamPublicLobbyApi lobbyApi,
        ServerVisibility visibility,
        ISteamLobbyLeaseRenewer leaseRenewer)
        : base(lobbyApi)
    {
        if (!Enum.IsDefined(typeof(ServerVisibility), visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));
        if (leaseRenewer == null) throw new ArgumentNullException(nameof(leaseRenewer));

        publicLobbyApi = lobbyApi;
        this.visibility = visibility;
        this.leaseRenewer = leaseRenewer;
        LobbyChanged += HandleLobbyChanged;
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
        activeInfo = info;
        CancelRetry();
        retryCount = 0;
        retryInfo = null;
        base.Advertise(info);
    }

    protected override bool ApplyAdditionalLobbyData(ulong targetLobbyId)
    {
        bool applied = lobbyApi.SetLobbyData(
            targetLobbyId,
            LobbyDataCodec.VisibilityKey,
            LobbyDataCodec.EncodeVisibility(visibility));
        bool hasExpiry = TryGetAdvertisementExpiry(out uint expiresAt);
        if (!hasExpiry && leasePublished) return applied;

        bool leaseApplied = lobbyApi.SetLobbyData(
            targetLobbyId,
            LobbyDataCodec.AdvertisementExpiresAtKey,
            LobbyDataCodec.EncodeAdvertisementExpiry(hasExpiry ? expiresAt : uint.MaxValue));
        if (leaseApplied)
        {
            leasePublished = true;
            consecutiveRenewalFailures = 0;
        }
        return applied && leaseApplied;
    }

    private bool TryGetAdvertisementExpiry(out uint expiresAt)
    {
        uint serverTime = publicLobbyApi.ServerRealTime;
        if (serverTime == 0)
        {
            expiresAt = 0;
            return false;
        }

        expiresAt = serverTime > uint.MaxValue - AdvertisementLeaseSeconds
            ? uint.MaxValue
            : serverTime + AdvertisementLeaseSeconds;
        return true;
    }

    private void HandleLobbyChanged(ulong lobbyId)
    {
        if (lobbyId == 0)
        {
            leasePublished = false;
            leaseRenewer.Stop();
            return;
        }

        retryCount = 0;
        consecutiveRenewalFailures = 0;
        retryInfo = null;
        leaseRenewer.Start(RenewLease);
    }

    private void RenewLease()
    {
        SessionJoinInfo info = activeInfo;
        if (standaloneDisposed || info == null || LobbyId == 0) return;
        if (!TryGetAdvertisementExpiry(out uint expiresAt))
        {
            Logger.Warning("Steam server time is unavailable; preserving the current lobby lease");
            return;
        }

        bool renewed;
        try
        {
            renewed = lobbyApi.SetLobbyData(
                LobbyId,
                LobbyDataCodec.AdvertisementExpiresAtKey,
                LobbyDataCodec.EncodeAdvertisementExpiry(expiresAt));
        }
        catch (Exception ex)
        {
            renewed = false;
            Logger.Error(ex, "Steam lobby lease renewal threw an exception");
        }

        if (renewed)
        {
            consecutiveRenewalFailures = 0;
            return;
        }

        consecutiveRenewalFailures++;
        if (consecutiveRenewalFailures < MaxConsecutiveRenewalFailures)
        {
            Logger.Warning(
                "Steam lobby lease renewal failed ({FailureCount}/{FailureLimit}); keeping the current advertisement",
                consecutiveRenewalFailures,
                MaxConsecutiveRenewalFailures);
            return;
        }

        Logger.Error("Steam lobby lease renewal failed repeatedly; recreating the advertisement");
        base.StopAdvertising();
        OnLobbyUnavailable(info);
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
        activeInfo = null;
        retryInfo = null;
        retryCount = 0;
        consecutiveRenewalFailures = 0;
        CancelRetry();
        leaseRenewer.Stop();
        base.StopAdvertising();
    }

    public override void Dispose()
    {
        if (standaloneDisposed) return;
        standaloneDisposed = true;
        activeInfo = null;
        retryInfo = null;
        CancelRetry();
        leaseRenewer.Stop();
        LobbyChanged -= HandleLobbyChanged;
        base.Dispose();
        leaseRenewer.Dispose();
    }

    private void CancelRetry()
    {
        retryTimer?.Dispose();
        retryTimer = null;
    }
}
