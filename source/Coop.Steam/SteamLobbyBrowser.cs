using Common.Network.Session;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Coop.Steam;

/// <summary>
/// Unions public search results with same-app friend lobbies and converts them into
/// display-safe standalone-server summaries.
/// </summary>
public class SteamLobbyBrowser : ISessionBrowser
{
    private readonly ISteamPublicLobbyApi lobbyApi;
    private bool requestInFlight;
    private LobbyRequest activeRequest;

    public SteamLobbyBrowser(ISteamPublicLobbyApi lobbyApi)
    {
        this.lobbyApi = lobbyApi;
    }

    public string Provider => "steam";
    public string DisplayName => "Steam";
    public bool IsAvailable => true;

    public void RequestSessions(Action<IReadOnlyList<SessionListing>, string> onCompleted)
    {
        if (requestInFlight)
        {
            onCompleted(Array.Empty<SessionListing>(), "A Steam lobby search is already in progress");
            return;
        }

        requestInFlight = true;
        var request = new LobbyRequest(onCompleted);
        activeRequest = request;

        try
        {
            var friendLobbyIds = lobbyApi.GetFriendLobbyIds();
            if (friendLobbyIds != null)
            {
                foreach (var lobbyId in friendLobbyIds)
                {
                    if (lobbyId != 0 && request.SeenFriendLobbyIds.Add(lobbyId))
                    {
                        request.FriendLobbyIds.Add(lobbyId);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Public discovery remains useful when Steam friend presence is unavailable.
        }

        try
        {
            lobbyApi.RequestLobbyList((lobbyIds, success) => CompletePublicRequest(request, lobbyIds, success));
        }
        catch (Exception)
        {
            FinishRequest(request, Array.Empty<SessionListing>(), "Could not retrieve Steam lobbies");
        }
    }

    private void CompletePublicRequest(LobbyRequest request, IReadOnlyList<ulong> lobbyIds, bool success)
    {
        if (!ReferenceEquals(activeRequest, request)) return;

        if (!success)
        {
            FinishRequest(request, Array.Empty<SessionListing>(), "Could not retrieve Steam lobbies");
            return;
        }

        var publicLobbyIds = new HashSet<ulong>();
        if (lobbyIds != null)
        {
            foreach (var lobbyId in lobbyIds)
            {
                if (lobbyId != 0 && publicLobbyIds.Add(lobbyId)) request.PublicLobbyIds.Add(lobbyId);
            }
        }

        foreach (var friendLobbyId in request.FriendLobbyIds)
        {
            if (!publicLobbyIds.Contains(friendLobbyId)) request.PendingFriendLobbyIds.Add(friendLobbyId);
        }

        if (request.PendingFriendLobbyIds.Count == 0)
        {
            FinishSuccessfulRequest(request);
            return;
        }

        // Populate the entire pending set before making any request: test doubles and failure paths
        // may complete synchronously, while Steam itself completes on a later callback pump.
        foreach (var friendLobbyId in request.FriendLobbyIds)
        {
            if (!request.PendingFriendLobbyIds.Contains(friendLobbyId)) continue;

            try
            {
                lobbyApi.RequestLobbyData(friendLobbyId,
                    dataLoaded => CompleteFriendLobbyData(request, friendLobbyId, dataLoaded));
            }
            catch (Exception)
            {
                CompleteFriendLobbyData(request, friendLobbyId, false);
            }
        }
    }

    private void CompleteFriendLobbyData(LobbyRequest request, ulong lobbyId, bool success)
    {
        if (!ReferenceEquals(activeRequest, request) || !request.PendingFriendLobbyIds.Remove(lobbyId)) return;

        if (success) request.LoadedFriendLobbyIds.Add(lobbyId);
        if (request.PendingFriendLobbyIds.Count == 0) FinishSuccessfulRequest(request);
    }

    private void FinishSuccessfulRequest(LobbyRequest request)
    {
        var lobbyIds = new List<ulong>(request.PublicLobbyIds.Count + request.LoadedFriendLobbyIds.Count);
        var seenLobbyIds = new HashSet<ulong>();
        foreach (var lobbyId in request.PublicLobbyIds)
        {
            if (seenLobbyIds.Add(lobbyId)) lobbyIds.Add(lobbyId);
        }

        foreach (var lobbyId in request.FriendLobbyIds)
        {
            if (request.LoadedFriendLobbyIds.Contains(lobbyId) && seenLobbyIds.Add(lobbyId)) lobbyIds.Add(lobbyId);
        }

        IReadOnlyList<SessionListing> summaries;
        try
        {
            summaries = BuildSummaries(lobbyIds);
        }
        catch (Exception)
        {
            FinishRequest(request, Array.Empty<SessionListing>(), "Could not retrieve Steam lobbies");
            return;
        }

        FinishRequest(request, summaries, null);
    }

    private IReadOnlyList<SessionListing> BuildSummaries(IReadOnlyList<ulong> lobbyIds)
    {
        uint serverTime = lobbyApi.ServerRealTime;
        var candidates = new List<LobbyCandidate>();
        foreach (var lobbyId in lobbyIds)
        {
            if (!SessionListingDataCodec.TryDecodeVisibility(
                    lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.VisibilityKey),
                    out var visibility) ||
                visibility == ServerVisibility.None)
            {
                continue;
            }

            if (!string.Equals(
                lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.ListingTypeKey),
                SessionListingDataCodec.DedicatedListingType,
                StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(
                    lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.TunnelProviderKey),
                    Provider,
                    StringComparison.Ordinal) ||
                !ulong.TryParse(lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.TunnelPeerIdKey),
                out var serverSteamId) || serverSteamId == 0)
            {
                continue;
            }

            bool hasValidLease = SessionListingDataCodec.TryDecodeAdvertisementExpiry(
                lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.AdvertisementExpiresAtKey),
                out uint expiresAt);
            if (serverTime != 0 && hasValidLease && expiresAt <= serverTime)
            {
                continue;
            }

            int.TryParse(lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.VersionKey), out var protocolVersion);
            var passwordRequired = lobbyApi.GetLobbyData(
                lobbyId, SessionListingDataCodec.PasswordRequiredKey) == "1";
            int.TryParse(lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.ConnectedPlayersKey),
                out var connectedPlayers);
            connectedPlayers = Math.Max(0, connectedPlayers);

            candidates.Add(new LobbyCandidate(
                serverSteamId,
                hasValidLease,
                expiresAt,
                new SessionListing
                {
                    Id = new SessionListingId("steam", lobbyId.ToString(CultureInfo.InvariantCulture)),
                    OwnerName = lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.OwnerNameKey),
                    ProtocolVersion = protocolVersion,
                    ModVersion = lobbyApi.GetLobbyData(lobbyId, SessionListingDataCodec.ModVersionKey),
                    PasswordRequired = passwordRequired,
                    ConnectedPlayers = connectedPlayers,
                }));
        }

        var latestExpiryByServer = new Dictionary<ulong, uint>();
        foreach (var candidate in candidates)
        {
            if (!candidate.HasValidLease) continue;
            if (!latestExpiryByServer.TryGetValue(candidate.ServerSteamId, out uint latestExpiry) ||
                candidate.ExpiresAt > latestExpiry)
            {
                latestExpiryByServer[candidate.ServerSteamId] = candidate.ExpiresAt;
            }
        }

        var summaries = new List<SessionListing>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!candidate.HasValidLease ||
                candidate.ExpiresAt == latestExpiryByServer[candidate.ServerSteamId])
            {
                summaries.Add(candidate.Summary);
            }
        }

        return summaries;
    }

    private void FinishRequest(LobbyRequest request, IReadOnlyList<SessionListing> summaries, string error)
    {
        if (!ReferenceEquals(activeRequest, request)) return;

        activeRequest = null;
        requestInFlight = false;
        request.OnCompleted(summaries, error);
    }

    private sealed class LobbyRequest
    {
        public readonly Action<IReadOnlyList<SessionListing>, string> OnCompleted;
        public readonly List<ulong> PublicLobbyIds = new List<ulong>();
        public readonly List<ulong> FriendLobbyIds = new List<ulong>();
        public readonly HashSet<ulong> SeenFriendLobbyIds = new HashSet<ulong>();
        public readonly HashSet<ulong> PendingFriendLobbyIds = new HashSet<ulong>();
        public readonly HashSet<ulong> LoadedFriendLobbyIds = new HashSet<ulong>();

        public LobbyRequest(Action<IReadOnlyList<SessionListing>, string> onCompleted)
        {
            OnCompleted = onCompleted;
        }
    }

    private sealed class LobbyCandidate
    {
        public readonly ulong ServerSteamId;
        public readonly bool HasValidLease;
        public readonly uint ExpiresAt;
        public readonly SessionListing Summary;

        public LobbyCandidate(
            ulong serverSteamId,
            bool hasValidLease,
            uint expiresAt,
            SessionListing summary)
        {
            ServerSteamId = serverSteamId;
            HasValidLease = hasValidLease;
            ExpiresAt = expiresAt;
            Summary = summary;
        }
    }
}
