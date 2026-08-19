using Common.Network.Session;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Coop.GOG;

/// <summary>Lists GOG Galaxy lobbies without exposing Galaxy SDK types.</summary>
public sealed class GalaxyLobbyBrowser : ISessionBrowser
{
    private readonly IGalaxySdk sdk;
    private bool requestInFlight;

    internal GalaxyLobbyBrowser(IGalaxySdk sdk)
    {
        this.sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
    }

    public string Provider => GalaxySessionProvider.ProviderId;
    public string DisplayName => "GOG";
    public bool IsAvailable => true;

    public void RequestSessions(Action<IReadOnlyList<SessionListing>, string> onCompleted)
    {
        if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
        if (requestInFlight)
        {
            onCompleted(Array.Empty<SessionListing>(), "A GOG lobby search is already in progress");
            return;
        }

        requestInFlight = true;
        try
        {
            sdk.EnsureAuthenticated(authenticated =>
            {
                if (!authenticated)
                {
                    Finish(
                        Array.Empty<SessionListing>(),
                        "GOG Galaxy is not signed in; launch Bannerlord through GOG Galaxy and try again",
                        onCompleted);
                    return;
                }

                RequestAuthenticatedSessions(onCompleted);
            });
        }
        catch (Exception ex)
        {
            requestInFlight = false;
            onCompleted(Array.Empty<SessionListing>(), $"Could not search GOG lobbies: {ex.Message}");
        }
    }

    private void RequestAuthenticatedSessions(
        Action<IReadOnlyList<SessionListing>, string> onCompleted)
    {
        try
        {
            sdk.RequestLobbyList((lobbyIds, success) =>
                HandleLobbyList(lobbyIds, success, onCompleted));
        }
        catch (Exception ex)
        {
            Finish(
                Array.Empty<SessionListing>(),
                $"Could not search GOG lobbies: {ex.Message}",
                onCompleted);
        }
    }

    private void HandleLobbyList(
        IReadOnlyList<ulong> lobbyIds,
        bool success,
        Action<IReadOnlyList<SessionListing>, string> onCompleted)
    {
        if (!success)
        {
            Finish(Array.Empty<SessionListing>(), "Could not retrieve GOG lobbies", onCompleted);
            return;
        }

        lobbyIds ??= Array.Empty<ulong>();
        var distinctLobbyIds = new HashSet<ulong>();
        foreach (ulong lobbyId in lobbyIds)
        {
            if (lobbyId != 0) distinctLobbyIds.Add(lobbyId);
        }

        if (distinctLobbyIds.Count == 0)
        {
            Finish(Array.Empty<SessionListing>(), null, onCompleted);
            return;
        }

        var listings = new List<SessionListing>();
        int remaining = distinctLobbyIds.Count;
        foreach (ulong lobbyId in distinctLobbyIds)
        {
            try
            {
                sdk.RequestLobbyData(lobbyId, loaded =>
                {
                    try
                    {
                        if (loaded && TryBuildListing(lobbyId, out var listing)) listings.Add(listing);
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        if (--remaining == 0) Finish(listings, null, onCompleted);
                    }
                });
            }
            catch (Exception)
            {
                if (--remaining == 0) Finish(listings, null, onCompleted);
            }
        }
    }

    private bool TryBuildListing(ulong lobbyId, out SessionListing listing)
    {
        listing = null;
        string listingType = sdk.GetLobbyData(lobbyId, SessionListingDataCodec.ListingTypeKey);
        if (!string.Equals(listingType, SessionListingDataCodec.DedicatedListingType, StringComparison.Ordinal) &&
            !string.Equals(listingType, SessionListingDataCodec.PlayerListingType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                sdk.GetLobbyData(lobbyId, SessionListingDataCodec.TunnelProviderKey),
                Provider,
                StringComparison.Ordinal) ||
            !ulong.TryParse(
                sdk.GetLobbyData(lobbyId, SessionListingDataCodec.TunnelPeerIdKey),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong tunnelPeerId) ||
            tunnelPeerId == 0 ||
            sdk.GetLobbyOwner(lobbyId) != tunnelPeerId)
        {
            return false;
        }

        if (SessionListingDataCodec.TryDecodeAdvertisementExpiry(
                sdk.GetLobbyData(lobbyId, SessionListingDataCodec.AdvertisementExpiresAtKey),
                out uint expiresAt) &&
            expiresAt <= sdk.UtcNowSeconds)
        {
            return false;
        }

        int.TryParse(
            sdk.GetLobbyData(lobbyId, SessionListingDataCodec.VersionKey),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int protocolVersion);
        int.TryParse(
            sdk.GetLobbyData(lobbyId, SessionListingDataCodec.ConnectedPlayersKey),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int connectedPlayers);

        listing = new SessionListing
        {
            Id = new SessionListingId(
                Provider,
                lobbyId.ToString(CultureInfo.InvariantCulture)),
            OwnerName = sdk.GetLobbyData(lobbyId, SessionListingDataCodec.OwnerNameKey),
            ProtocolVersion = protocolVersion,
            ModVersion = sdk.GetLobbyData(lobbyId, SessionListingDataCodec.ModVersionKey),
            PasswordRequired = sdk.GetLobbyData(
                lobbyId,
                SessionListingDataCodec.PasswordRequiredKey) == "1",
            ConnectedPlayers = Math.Max(0, connectedPlayers),
        };
        return true;
    }

    private void Finish(
        IReadOnlyList<SessionListing> listings,
        string error,
        Action<IReadOnlyList<SessionListing>, string> onCompleted)
    {
        if (!requestInFlight) return;
        requestInFlight = false;
        onCompleted(listings, error);
    }
}
