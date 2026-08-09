using Common.Network.Session;
using Coop.Steam;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Steam;

public class SteamLobbyBrowserTests
{
    private readonly FakeSteamLobbyApi api = new FakeSteamLobbyApi();
    private readonly SteamLobbyBrowser browser;

    public SteamLobbyBrowserTests()
    {
        browser = new SteamLobbyBrowser(api);
    }

    private void AddLobby(ulong lobbyId, SessionJoinInfo info, bool publiclyListed = true)
    {
        info.ModVersion ??= Common.ModInformation.BuildVersion;
        info.DedicatedServer = true;
        if (publiclyListed) api.ListedLobbyIds.Add(lobbyId);
        foreach (var pair in SessionListingDataCodec.Encode(info))
        {
            api.SetLobbyData(lobbyId, pair.Key, pair.Value);
        }
        api.SetLobbyData(lobbyId, SessionListingDataCodec.OwnerNameKey, api.PersonaName);
        api.SetLobbyData(lobbyId, SessionListingDataCodec.VisibilityKey, "public");
    }

    private static PlatformIdentity SteamIdentity(ulong steamId) =>
        new PlatformIdentity(SteamSessionProvider.ProviderId, steamId.ToString());

    private void SetAdvertisementExpiry(ulong lobbyId, uint expiresAt)
    {
        api.SetLobbyData(lobbyId, SessionListingDataCodec.AdvertisementExpiresAtKey,
            SessionListingDataCodec.EncodeAdvertisementExpiry(expiresAt));
    }

    [Fact]
    public void RequestSessions_ReturnsStandaloneMetadata()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            TunnelTarget = SteamIdentity(76561198000000042),
            ModVersion = Common.ModInformation.BuildVersion,
            PasswordRequired = true,
            ConnectedPlayers = 3,
        });

        IReadOnlyList<SessionListing> results = null;
        string error = null;
        browser.RequestSessions((lobbies, failure) => (results, error) = (lobbies, failure));

        var lobby = Assert.Single(results);
        Assert.Equal("42", lobby.Id.Value);
        Assert.Equal(api.PersonaName, lobby.OwnerName);
        Assert.Equal(SessionJoinInfo.CurrentVersion, lobby.ProtocolVersion);
        Assert.Equal(Common.ModInformation.BuildVersion, lobby.ModVersion);
        Assert.True(lobby.PasswordRequired);
        Assert.Equal(3, lobby.ConnectedPlayers);
        Assert.True(lobby.IsCompatible);
        Assert.Null(error);
    }

    [Fact]
    public void RequestSessions_UnionsFriendLobbiesAndDeduplicatesPublicMatches()
    {
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) },
            publiclyListed: false);
        api.FriendLobbyIds.Add(41);
        api.FriendLobbyIds.Add(42);
        api.FriendLobbyIds.Add(42);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Collection(results,
            lobby => Assert.Equal("41", lobby.Id.Value),
            lobby => Assert.Equal("42", lobby.Id.Value));
        Assert.Equal(new[] { 42UL }, api.RequestedLobbyDataIds);
    }

    [Fact]
    public void RequestSessions_ExcludesExpiredAdvertisementAndKeepsRestartedServer()
    {
        api.SteamServerTime = 1_000;
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        SetAdvertisementExpiry(41, 1_000);
        SetAdvertisementExpiry(42, 1_060);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal("42", Assert.Single(results).Id.Value);
    }

    [Fact]
    public void RequestSessions_KeepsMissingAndMalformedAdvertisementLeases()
    {
        api.SteamServerTime = 1_000;
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        api.SetLobbyData(42, SessionListingDataCodec.AdvertisementExpiresAtKey, "not-a-time");

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Collection(results,
            lobby => Assert.Equal("41", lobby.Id.Value),
            lobby => Assert.Equal("42", lobby.Id.Value));
    }

    [Fact]
    public void RequestSessions_UnavailableSteamTimeFailsOpen()
    {
        api.SteamServerTime = 0;
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        SetAdvertisementExpiry(42, 1);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal("42", Assert.Single(results).Id.Value);
    }

    [Fact]
    public void RequestSessions_SameServerSteamIdKeepsLatestAdvertisement()
    {
        const ulong serverSteamId = 76561198000000042;
        api.SteamServerTime = 1_000;
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(serverSteamId) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(serverSteamId) });
        SetAdvertisementExpiry(41, 1_030);
        SetAdvertisementExpiry(42, 1_060);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal("42", Assert.Single(results).Id.Value);
    }

    [Fact]
    public void RequestSessions_TiedAdvertisementLeasesFailOpen()
    {
        const ulong serverSteamId = 76561198000000042;
        api.SteamServerTime = 1_000;
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(serverSteamId) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(serverSteamId) });
        SetAdvertisementExpiry(41, 1_060);
        SetAdvertisementExpiry(42, 1_060);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void RequestSessions_DistinctLiveServerSteamIdsRemainDistinct()
    {
        api.SteamServerTime = 1_000;
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        SetAdvertisementExpiry(41, 1_030);
        SetAdvertisementExpiry(42, 1_060);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Collection(results,
            lobby => Assert.Equal("41", lobby.Id.Value),
            lobby => Assert.Equal("42", lobby.Id.Value));
    }

    [Fact]
    public void RequestSessions_PublicAndFriendDuplicatesUseLatestAdvertisement()
    {
        const ulong serverSteamId = 76561198000000042;
        api.SteamServerTime = 1_000;
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(serverSteamId) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(serverSteamId) },
            publiclyListed: false);
        SetAdvertisementExpiry(41, 1_030);
        SetAdvertisementExpiry(42, 1_060);
        api.FriendLobbyIds.Add(42);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal("42", Assert.Single(results).Id.Value);
    }

    [Fact]
    public void RequestSessions_HidesNoneVisibilityButKeepsOlderLobbiesDiscoverable()
    {
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        api.SetLobbyData(42, SessionListingDataCodec.VisibilityKey, "none");

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal("41", Assert.Single(results).Id.Value);
    }

    [Fact]
    public void RequestSessions_HidesHiddenStandaloneObtainedThroughFriendPresence()
    {
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) },
            publiclyListed: false);
        api.SetLobbyData(42, SessionListingDataCodec.ListingTypeKey, SessionListingDataCodec.HiddenDedicatedListingType);
        api.FriendLobbyIds.Add(42);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Empty(results);
    }

    [Fact]
    public void RequestSessions_WaitsForFriendLobbyMetadata()
    {
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) },
            publiclyListed: false);
        api.FriendLobbyIds.Add(42);
        api.CompleteLobbyDataRequestsImmediately = false;

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Null(results);
        Assert.Equal(new[] { 42UL }, api.RequestedLobbyDataIds);

        api.CompletePendingLobbyData(42);

        Assert.Equal("42", Assert.Single(results).Id.Value);
    }

    [Fact]
    public void RequestSessions_SkipsFriendLobbyWhoseMetadataCouldNotBeLoaded()
    {
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) },
            publiclyListed: false);
        api.FriendLobbyIds.Add(42);
        api.FailedLobbyDataRequests.Add(42);

        IReadOnlyList<SessionListing> results = null;
        string error = null;
        browser.RequestSessions((lobbies, failure) => (results, error) = (lobbies, failure));

        Assert.Equal("41", Assert.Single(results).Id.Value);
        Assert.Null(error);
    }

    [Fact]
    public void RequestSessions_FriendPresenceFailureStillReturnsPublicLobbies()
    {
        AddLobby(41, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000041) });
        api.ThrowOnFriendLobbyRequest = true;

        IReadOnlyList<SessionListing> results = null;
        string error = null;
        browser.RequestSessions((lobbies, failure) => (results, error) = (lobbies, failure));

        Assert.Equal("41", Assert.Single(results).Id.Value);
        Assert.Null(error);
    }

    [Fact]
    public void RequestSessions_SynchronousFriendDataFailureDoesNotBlockRetry()
    {
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) },
            publiclyListed: false);
        api.FriendLobbyIds.Add(42);
        api.ThrowOnLobbyDataRequest = true;

        IReadOnlyList<SessionListing> first = null;
        browser.RequestSessions((lobbies, _) => first = lobbies);

        api.ThrowOnLobbyDataRequest = false;
        IReadOnlyList<SessionListing> retry = null;
        browser.RequestSessions((lobbies, _) => retry = lobbies);

        Assert.Empty(first);
        Assert.Equal("42", Assert.Single(retry).Id.Value);
    }

    [Fact]
    public void RequestSessions_PreservesMissingOwnerNameForUiFallback()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            TunnelTarget = SteamIdentity(76561198000000042),
        });
        api.SetLobbyData(42, SessionListingDataCodec.OwnerNameKey, string.Empty);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal(string.Empty, Assert.Single(results).OwnerName);
    }

    [Fact]
    public void RequestSessions_FiltersPlayerAndMalformedLobbies()
    {
        AddLobby(41, new SessionJoinInfo { Port = 4200 });
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        api.SetLobbyData(42, SessionListingDataCodec.TunnelPeerIdKey, "not-an-id");

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Empty(results);
    }

    [Fact]
    public void RequestSessions_PreservesIncompatibleVersionForDisplay()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Version = SessionJoinInfo.CurrentVersion + 1,
            Port = 4200,
            TunnelTarget = SteamIdentity(76561198000000042),
        });

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.False(Assert.Single(results).IsCompatible);
    }

    [Fact]
    public void RequestSessions_PreservesDifferentModVersionButMarksItIncompatible()
    {
        const string otherVersion = "different-build";
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            TunnelTarget = SteamIdentity(76561198000000042),
            ModVersion = otherVersion,
        });

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        var lobby = Assert.Single(results);
        Assert.Equal(otherVersion, lobby.ModVersion);
        Assert.False(lobby.IsCompatible);
    }

    [Fact]
    public void RequestSessions_TreatsTextualPasswordFlagAsFalse()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            TunnelTarget = SteamIdentity(76561198000000042),
            PasswordRequired = true,
        });
        api.SetLobbyData(42, SessionListingDataCodec.PasswordRequiredKey, "true");

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.False(Assert.Single(results).PasswordRequired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-count")]
    [InlineData("-1")]
    public void RequestSessions_UsesZeroForInvalidConnectedPlayerCount(string value)
    {
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            TunnelTarget = SteamIdentity(76561198000000042),
        });
        api.SetLobbyData(42, SessionListingDataCodec.ConnectedPlayersKey, value);

        IReadOnlyList<SessionListing> results = null;
        browser.RequestSessions((lobbies, _) => results = lobbies);

        Assert.Equal(0, Assert.Single(results).ConnectedPlayers);
    }

    [Fact]
    public void RequestSessions_ReportsSteamFailure()
    {
        api.ListSucceeds = false;

        string error = null;
        browser.RequestSessions((_, failure) => error = failure);

        Assert.NotNull(error);
    }

    [Fact]
    public void RequestSessions_SynchronousFailureDoesNotBlockRetry()
    {
        api.ThrowOnListRequest = true;

        string firstError = null;
        browser.RequestSessions((_, failure) => firstError = failure);

        api.ThrowOnListRequest = false;
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });
        IReadOnlyList<SessionListing> retry = null;
        browser.RequestSessions((lobbies, _) => retry = lobbies);

        Assert.NotNull(firstError);
        Assert.Single(retry);
    }

    [Fact]
    public void RequestSessions_RejectsOverlappingRefreshWithoutReplacingFirst()
    {
        api.CompleteOperationsImmediately = false;
        AddLobby(42, new SessionJoinInfo { Port = 4200, TunnelTarget = SteamIdentity(76561198000000042) });

        IReadOnlyList<SessionListing> first = null;
        string secondError = null;
        browser.RequestSessions((lobbies, _) => first = lobbies);
        browser.RequestSessions((_, failure) => secondError = failure);

        api.CompletePendingList();

        Assert.Single(first);
        Assert.NotNull(secondError);
    }
}
