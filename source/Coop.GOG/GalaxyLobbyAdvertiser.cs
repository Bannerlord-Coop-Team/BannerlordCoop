using Common;
using Common.Logging;
using Common.Network.Session;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Coop.GOG;

/// <summary>Advertises a co-op session through one GOG Galaxy lobby.</summary>
public sealed class GalaxyLobbyAdvertiser : ISessionAdvertiser, ISessionAdvertisementOwner
{
    private static readonly ILogger Logger = LogManager.GetLogger<GalaxyLobbyAdvertiser>();

    public const int MaxLobbyMembers = 128;
    public const string ConnectLobbyArgument = "+connect_gog_lobby";
    internal const uint AdvertisementLeaseSeconds = 60;
    internal static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan CreateRetryInterval = TimeSpan.FromSeconds(5);

    private readonly IGalaxySdk sdk;
    private readonly ISessionMembership membership;
    private readonly ServerVisibility visibility;
    private readonly bool dedicatedServer;
    private ulong lobbyId;
    private bool createInFlight;
    private bool dataWriteInFlight;
    private bool dataRefreshPending;
    private bool leaseRenewalInFlight;
    private bool listingPublished;
    private bool richPresenceSet;
    private bool disposed;
    private int dataWriteGeneration;
    private SessionJoinInfo pendingInfo;
    private Timer leaseRenewalTimer;
    private Timer createRetryTimer;

    internal GalaxyLobbyAdvertiser(
        IGalaxySdk sdk,
        ISessionMembership membership,
        ServerVisibility visibility,
        bool dedicatedServer)
    {
        if (sdk == null) throw new ArgumentNullException(nameof(sdk));
        if (!Enum.IsDefined(typeof(ServerVisibility), visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        this.sdk = sdk;
        this.membership = membership;
        this.visibility = visibility;
        this.dedicatedServer = dedicatedServer;
    }

    public bool IsAdvertising => lobbyId != 0 && listingPublished;
    public bool CanInviteFriends => IsAdvertising || membership?.IsInSession == true;
    public SessionListingId ListingId => !IsAdvertising
        ? default
        : new SessionListingId(
            GalaxySessionProvider.ProviderId,
            lobbyId.ToString(CultureInfo.InvariantCulture));

    public event Action<SessionListingId> ListingChanged;

    public void Advertise(SessionJoinInfo info)
    {
        if (disposed || info == null) return;

        info.DedicatedServer = dedicatedServer;
        info.Discoverable = visibility != ServerVisibility.None;
        pendingInfo = info;
        if (lobbyId != 0)
        {
            ApplyLobbyData();
            return;
        }

        if (createInFlight) return;
        CancelCreateRetry();
        createInFlight = true;
        try
        {
            sdk.CreateLobby(ToGalaxyVisibility(), MaxLobbyMembers, HandleLobbyCreated);
        }
        catch (Exception ex)
        {
            createInFlight = false;
            Logger.Error(ex, "Could not request a GOG Galaxy lobby");
            ScheduleCreateRetry();
        }
    }

    private void HandleLobbyCreated(ulong createdLobbyId, bool success)
    {
        createInFlight = false;
        if (!success || createdLobbyId == 0)
        {
            Logger.Error("Could not create a GOG Galaxy lobby; invites are unavailable until the next retry");
            ScheduleCreateRetry();
            return;
        }

        if (disposed || pendingInfo == null)
        {
            LeaveLobby(createdLobbyId);
            return;
        }

        lobbyId = createdLobbyId;
        CancelCreateRetry();
        ApplyLobbyData();
    }

    private void ApplyLobbyData()
    {
        if (disposed || lobbyId == 0 || pendingInfo == null) return;
        if (dataWriteInFlight)
        {
            dataRefreshPending = true;
            return;
        }

        var values = new Dictionary<string, string>();
        try
        {
            foreach (var pair in SessionListingDataCodec.Encode(pendingInfo))
                values[pair.Key] = pair.Value;

            values[SessionListingDataCodec.OwnerNameKey] = sdk.LocalPersonaName ?? string.Empty;
            values[SessionListingDataCodec.VisibilityKey] =
                SessionListingDataCodec.EncodeVisibility(visibility);
            values[SessionListingDataCodec.AdvertisementExpiresAtKey] =
                CurrentAdvertisementExpiry();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GOG Galaxy lobby data writes threw an exception");
            RecoverAdvertisement();
            return;
        }

        ulong targetLobbyId = lobbyId;
        int generation = ++dataWriteGeneration;
        int remaining = values.Count;
        bool allSucceeded = true;
        dataWriteInFlight = true;
        dataRefreshPending = false;

        foreach (var pair in values)
        {
            try
            {
                sdk.SetLobbyData(targetLobbyId, pair.Key, pair.Value, success =>
                {
                    allSucceeded &= success;
                    remaining--;
                    if (remaining == 0)
                        CompleteLobbyDataWrite(targetLobbyId, generation, allSucceeded);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GOG Galaxy lobby data write for {Key} threw an exception", pair.Key);
                allSucceeded = false;
                remaining--;
                if (remaining == 0)
                    CompleteLobbyDataWrite(targetLobbyId, generation, allSucceeded);
            }
        }
    }

    private void CompleteLobbyDataWrite(ulong targetLobbyId, int generation, bool success)
    {
        if (disposed || targetLobbyId != lobbyId || generation != dataWriteGeneration) return;

        dataWriteInFlight = false;
        if (!success)
        {
            Logger.Error("GOG Galaxy lobby data writes failed; withdrawing the advertisement");
            RecoverAdvertisement();
            return;
        }

        if (dataRefreshPending)
        {
            ApplyLobbyData();
            return;
        }

        try
        {
            richPresenceSet |= sdk.SetRichPresenceConnect(BuildConnectString(lobbyId));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not set GOG Galaxy rich presence for the lobby");
        }

        StartLeaseRenewal();
        if (!listingPublished)
        {
            listingPublished = true;
            NotifyListingChanged(ListingId);
        }
    }

    internal void RenewAdvertisementLease()
    {
        if (disposed || lobbyId == 0 || pendingInfo == null ||
            dataWriteInFlight || leaseRenewalInFlight)
        {
            return;
        }

        ulong targetLobbyId = lobbyId;
        leaseRenewalInFlight = true;
        try
        {
            sdk.SetLobbyData(
                targetLobbyId,
                SessionListingDataCodec.AdvertisementExpiresAtKey,
                CurrentAdvertisementExpiry(),
                success => CompleteLeaseRenewal(targetLobbyId, success));
        }
        catch (Exception ex)
        {
            leaseRenewalInFlight = false;
            Logger.Error(ex, "Could not renew the GOG Galaxy lobby advertisement lease");
            RecoverAdvertisement();
        }
    }

    private void CompleteLeaseRenewal(ulong targetLobbyId, bool success)
    {
        if (disposed || targetLobbyId != lobbyId) return;

        leaseRenewalInFlight = false;
        if (!success) RecoverAdvertisement();
    }

    private string CurrentAdvertisementExpiry()
    {
        uint now = sdk.UtcNowSeconds;
        uint expiresAt = now > uint.MaxValue - AdvertisementLeaseSeconds
            ? uint.MaxValue
            : now + AdvertisementLeaseSeconds;
        return SessionListingDataCodec.EncodeAdvertisementExpiry(expiresAt);
    }

    private void StartLeaseRenewal()
    {
        if (leaseRenewalTimer != null) return;

        leaseRenewalTimer = new Timer(
            _ => GameThread.RunSafe(
                RenewAdvertisementLease,
                context: "RenewGalaxyLobbyLease"),
            null,
            LeaseRenewalInterval,
            LeaseRenewalInterval);
    }

    public bool InviteFriends()
    {
        ulong inviteLobbyId = IsAdvertising ? lobbyId : 0;
        if (inviteLobbyId == 0 &&
            membership?.ListingId is SessionListingId listingId &&
            string.Equals(listingId.Provider, GalaxySessionProvider.ProviderId, StringComparison.Ordinal))
        {
            ulong.TryParse(
                listingId.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out inviteLobbyId);
        }

        return inviteLobbyId != 0 && sdk.ShowInviteDialog(BuildConnectString(inviteLobbyId));
    }

    public void StopAdvertising()
    {
        WithdrawAdvertisement(clearPendingInfo: true);
    }

    private void RecoverAdvertisement()
    {
        WithdrawAdvertisement(clearPendingInfo: false);
        ScheduleCreateRetry();
    }

    private void WithdrawAdvertisement(bool clearPendingInfo)
    {
        if (clearPendingInfo) pendingInfo = null;
        CancelCreateRetry();
        leaseRenewalTimer?.Dispose();
        leaseRenewalTimer = null;
        ulong stoppedLobbyId = lobbyId;
        bool stoppedPublishedListing = listingPublished;
        lobbyId = 0;
        dataWriteGeneration++;
        dataWriteInFlight = false;
        dataRefreshPending = false;
        leaseRenewalInFlight = false;
        listingPublished = false;
        if (stoppedLobbyId != 0) LeaveLobby(stoppedLobbyId);

        if (richPresenceSet)
        {
            try
            {
                sdk.ClearRichPresenceConnect();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not clear GOG Galaxy lobby rich presence");
            }

            richPresenceSet = false;
        }

        if (stoppedPublishedListing) NotifyListingChanged(default);
    }

    private void ScheduleCreateRetry()
    {
        if (disposed || pendingInfo == null || lobbyId != 0 || createInFlight || createRetryTimer != null)
            return;

        createRetryTimer = new Timer(
            _ => GameThread.RunSafe(RetryCreate, context: "RetryGalaxyLobbyCreate"),
            null,
            CreateRetryInterval,
            Timeout.InfiniteTimeSpan);
    }

    internal void RetryCreate()
    {
        CancelCreateRetry();
        if (disposed || pendingInfo == null || lobbyId != 0 || createInFlight) return;

        Advertise(pendingInfo);
    }

    private void CancelCreateRetry()
    {
        createRetryTimer?.Dispose();
        createRetryTimer = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StopAdvertising();
    }

    internal static string BuildConnectString(ulong lobbyId) =>
        ConnectLobbyArgument + " " + lobbyId.ToString(CultureInfo.InvariantCulture);

    private void LeaveLobby(ulong targetLobbyId)
    {
        try
        {
            sdk.LeaveLobby(targetLobbyId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not leave GOG Galaxy lobby {LobbyId}", targetLobbyId.ToString());
        }
    }

    private void NotifyListingChanged(SessionListingId listingId)
    {
        try
        {
            ListingChanged?.Invoke(listingId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "A GOG Galaxy listing-change subscriber failed");
        }
    }

    private GalaxyLobbyVisibility ToGalaxyVisibility()
    {
        return visibility switch
        {
            ServerVisibility.Public => GalaxyLobbyVisibility.Public,
            ServerVisibility.FriendsOnly => GalaxyLobbyVisibility.FriendsOnly,
            ServerVisibility.None => GalaxyLobbyVisibility.Private,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
