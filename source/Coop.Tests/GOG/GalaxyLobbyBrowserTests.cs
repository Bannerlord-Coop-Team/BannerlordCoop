using Common;
using Common.Network.Session;
using Coop.GOG;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.GOG;

public class GalaxyLobbyBrowserTests
{
    [Fact]
    public void RequestSessions_WhenGalaxyAuthenticationFails_ReportsHowToRetry()
    {
        var sdk = new FakeGalaxySdk
        {
            LocalUserId = 0,
            AuthenticationSucceeds = false,
        };
        var browser = new GalaxyLobbyBrowser(sdk);
        IReadOnlyList<SessionListing> result = null;
        string error = null;

        browser.RequestSessions((listings, failure) =>
        {
            result = listings;
            error = failure;
        });

        Assert.Empty(result);
        Assert.Contains("launch Bannerlord through GOG Galaxy", error);
        Assert.Equal(1, sdk.AuthenticationRequests);
    }

    [Fact]
    public void RequestSessions_WhenPendingAuthenticationCompletes_ContinuesLobbySearch()
    {
        var sdk = new FakeGalaxySdk
        {
            LocalUserId = 0,
            CompleteAuthenticationImmediately = false,
        };
        var browser = new GalaxyLobbyBrowser(sdk);
        IReadOnlyList<SessionListing> result = null;
        string error = "not completed";

        browser.RequestSessions((listings, failure) =>
        {
            result = listings;
            error = failure;
        });

        Assert.Null(result);
        sdk.CompleteAuthentication(success: true);

        Assert.Empty(result);
        Assert.Null(error);
    }

    [Fact]
    public void RequestSessions_ReturnsDisplaySafeGogListing()
    {
        var sdk = new FakeGalaxySdk { LobbyList = new ulong[] { 42 } };
        ConfigureListing(sdk, 42, ownerId: 500, ownerName: "GOG Host", connectedPlayers: 3);
        var browser = new GalaxyLobbyBrowser(sdk);
        IReadOnlyList<SessionListing> result = null;
        string error = "not completed";

        browser.RequestSessions((listings, failure) =>
        {
            result = listings;
            error = failure;
        });

        var listing = Assert.Single(result);
        Assert.Null(error);
        Assert.Equal(new SessionListingId("gog", "42"), listing.Id);
        Assert.Equal("GOG Host", listing.OwnerName);
        Assert.Equal(3, listing.ConnectedPlayers);
        Assert.True(listing.IsCompatible);
    }

    [Fact]
    public void RequestSessions_FiltersProviderSpoofOwnerMismatchExpiredAndHiddenListings()
    {
        var sdk = new FakeGalaxySdk
        {
            LobbyList = new ulong[] { 0, 1, 1, 2, 3, 4, 5 },
            UtcNowSeconds = 1_000,
        };
        ConfigureListing(sdk, 1, ownerId: 101, ownerName: "valid");
        ConfigureListing(sdk, 2, ownerId: 102, ownerName: "wrong provider");
        sdk.SetLobbyData(2, SessionListingDataCodec.TunnelProviderKey, "steam");
        ConfigureListing(sdk, 3, ownerId: 103, ownerName: "wrong owner");
        sdk.SetLobbyData(3, SessionListingDataCodec.TunnelPeerIdKey, "999");
        ConfigureListing(sdk, 4, ownerId: 104, ownerName: "expired");
        sdk.SetLobbyData(4, SessionListingDataCodec.AdvertisementExpiresAtKey, "1000");
        ConfigureListing(sdk, 5, ownerId: 105, ownerName: "hidden", discoverable: false, dedicated: true);
        var browser = new GalaxyLobbyBrowser(sdk);
        IReadOnlyList<SessionListing> result = null;

        browser.RequestSessions((listings, _) => result = listings);

        Assert.Equal("valid", Assert.Single(result).OwnerName);
        Assert.Equal(new ulong[] { 1, 2, 3, 4, 5 }, sdk.LobbyDataRequests);
    }

    [Fact]
    public void RequestSessions_OverlappingRequestIsRejectedWithoutReplacingFirst()
    {
        var sdk = new FakeGalaxySdk
        {
            CompleteLobbyListImmediately = false,
            LobbyList = System.Array.Empty<ulong>(),
        };
        var browser = new GalaxyLobbyBrowser(sdk);
        int firstCompletions = 0;
        string secondError = null;
        browser.RequestSessions((_, _) => firstCompletions++);

        browser.RequestSessions((_, error) => secondError = error);
        sdk.CompleteLobbyList();

        Assert.Contains("already", secondError);
        Assert.Equal(1, firstCompletions);
    }

    [Fact]
    public void RequestSessions_LobbyReadFailureStillCompletesRequest()
    {
        var sdk = new FakeGalaxySdk
        {
            LobbyList = new ulong[] { 42 },
            ThrowOnLobbyDataRead = 42,
        };
        var browser = new GalaxyLobbyBrowser(sdk);
        int completions = 0;
        IReadOnlyList<SessionListing> result = null;

        browser.RequestSessions((listings, _) =>
        {
            completions++;
            result = listings;
        });

        Assert.Equal(1, completions);
        Assert.Empty(result);
    }

    internal static void ConfigureListing(
        FakeGalaxySdk sdk,
        ulong lobbyId,
        ulong ownerId,
        string ownerName,
        int connectedPlayers = 0,
        bool discoverable = true,
        bool dedicated = false)
    {
        var info = new SessionJoinInfo
        {
            Port = 4200,
            TunnelTarget = new PlatformIdentity("gog", ownerId.ToString()),
            ModVersion = ModInformation.BuildVersion,
            ConnectedPlayers = connectedPlayers,
            Discoverable = discoverable,
            DedicatedServer = dedicated,
        };
        foreach (var pair in SessionListingDataCodec.Encode(info))
            sdk.SetLobbyData(lobbyId, pair.Key, pair.Value);
        sdk.SetLobbyData(lobbyId, SessionListingDataCodec.OwnerNameKey, ownerName);
        sdk.SetLobbyData(
            lobbyId,
            SessionListingDataCodec.AdvertisementExpiresAtKey,
            SessionListingDataCodec.EncodeAdvertisementExpiry(sdk.UtcNowSeconds + 60));
        sdk.SetLobbyOwner(lobbyId, ownerId);
    }
}
