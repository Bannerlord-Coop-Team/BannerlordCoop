using Common;
using Common.Network.Session;
using Coop.GOG;
using Xunit;

namespace Coop.Tests.GOG;

public class GalaxyLobbyAdvertiserTests
{
    [Theory]
    [InlineData(ServerVisibility.Public, 2)]
    [InlineData(ServerVisibility.FriendsOnly, 1)]
    [InlineData(ServerVisibility.None, 0)]
    public void Advertise_CreatesVisibilityScopedLobbyWithProviderMetadata(
        ServerVisibility visibility,
        int expectedVisibility)
    {
        var sdk = new FakeGalaxySdk();
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            visibility,
            dedicatedServer: false);
        SessionListingId changed = default;
        advertiser.ListingChanged += listingId => changed = listingId;

        advertiser.Advertise(CreateInfo(sdk.LocalUserId));

        Assert.True(advertiser.IsAdvertising);
        Assert.Equal(new SessionListingId("gog", "42"), advertiser.ListingId);
        Assert.Equal(advertiser.ListingId, changed);
        Assert.Equal((GalaxyLobbyVisibility)expectedVisibility, Assert.Single(sdk.CreateRequests).Visibility);
        Assert.Equal(GalaxyLobbyAdvertiser.MaxLobbyMembers, sdk.CreateRequests[0].MaxMembers);
        Assert.Equal("gog", sdk.GetLobbyData(42, SessionListingDataCodec.TunnelProviderKey));
        Assert.Equal("100", sdk.GetLobbyData(42, SessionListingDataCodec.TunnelPeerIdKey));
        Assert.Equal(sdk.LocalPersonaName, sdk.GetLobbyData(42, SessionListingDataCodec.OwnerNameKey));
        Assert.Equal(
            SessionListingDataCodec.EncodeVisibility(visibility),
            sdk.GetLobbyData(42, SessionListingDataCodec.VisibilityKey));
        Assert.Contains(GalaxyLobbyAdvertiser.BuildConnectString(42), sdk.RichPresenceConnects);
    }

    [Fact]
    public void Advertise_CreationInFlightUsesLatestMetadata()
    {
        var sdk = new FakeGalaxySdk { CompleteCreateImmediately = false };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);
        var info = CreateInfo(sdk.LocalUserId);
        advertiser.Advertise(info);
        info.ConnectedPlayers = 4;
        advertiser.Advertise(info);

        sdk.CompleteCreate();

        Assert.Single(sdk.CreateRequests);
        Assert.Equal("4", sdk.GetLobbyData(42, SessionListingDataCodec.ConnectedPlayersKey));
    }

    [Fact]
    public void Advertise_WaitsForRequiredLobbyDataWritesBeforePublishing()
    {
        var sdk = new FakeGalaxySdk { CompleteLobbyDataWritesImmediately = false };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);
        SessionListingId changed = default;
        advertiser.ListingChanged += listingId => changed = listingId;

        advertiser.Advertise(CreateInfo(sdk.LocalUserId));

        Assert.False(advertiser.IsAdvertising);
        Assert.False(changed.IsValid);
        Assert.Empty(sdk.RichPresenceConnects);

        sdk.CompleteAllLobbyDataWrites();

        Assert.True(advertiser.IsAdvertising);
        Assert.Equal(new SessionListingId("gog", "42"), changed);
        Assert.Single(sdk.RichPresenceConnects);
    }

    [Fact]
    public void Advertise_AsynchronousLobbyDataFailureWithdrawsBeforePublishing()
    {
        var sdk = new FakeGalaxySdk
        {
            CompleteLobbyDataWritesImmediately = false,
            FailedLobbyDataKey = SessionListingDataCodec.PortKey,
        };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);
        int listingChanges = 0;
        advertiser.ListingChanged += _ => listingChanges++;

        advertiser.Advertise(CreateInfo(sdk.LocalUserId));
        sdk.CompleteAllLobbyDataWrites();

        Assert.False(advertiser.IsAdvertising);
        Assert.Contains(42UL, sdk.LeftLobbies);
        Assert.Equal(0, listingChanges);
        Assert.Empty(sdk.RichPresenceConnects);
    }

    [Fact]
    public void Advertise_CreateFailureCanRetry()
    {
        var sdk = new FakeGalaxySdk { CreateSucceeds = false };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);

        advertiser.Advertise(CreateInfo(sdk.LocalUserId));
        Assert.False(advertiser.IsAdvertising);

        sdk.CreateSucceeds = true;
        advertiser.RetryCreate();

        Assert.True(advertiser.IsAdvertising);
        Assert.Equal(2, sdk.CreateRequests.Count);
    }

    [Fact]
    public void Advertise_CreateExceptionCanRetry()
    {
        var sdk = new FakeGalaxySdk { ThrowOnCreate = true };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);

        advertiser.Advertise(CreateInfo(sdk.LocalUserId));
        Assert.False(advertiser.IsAdvertising);

        sdk.ThrowOnCreate = false;
        advertiser.RetryCreate();

        Assert.True(advertiser.IsAdvertising);
        Assert.Equal(2, sdk.CreateRequests.Count);
    }

    [Fact]
    public void Advertise_LobbyDataExceptionWithdrawsAndCanRetry()
    {
        var sdk = new FakeGalaxySdk
        {
            ThrowOnLobbyDataWriteKey = SessionListingDataCodec.PortKey,
        };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);

        advertiser.Advertise(CreateInfo(sdk.LocalUserId));

        Assert.False(advertiser.IsAdvertising);
        Assert.Contains(42UL, sdk.LeftLobbies);

        sdk.ThrowOnLobbyDataWriteKey = string.Empty;
        sdk.NextLobbyId = 43;
        advertiser.RetryCreate();

        Assert.True(advertiser.IsAdvertising);
        Assert.Equal(new SessionListingId("gog", "43"), advertiser.ListingId);
    }

    [Fact]
    public void RenewAdvertisementLease_ExtendsExpiryFromCurrentTime()
    {
        var sdk = new FakeGalaxySdk { UtcNowSeconds = 1_000 };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: true);
        advertiser.Advertise(CreateInfo(sdk.LocalUserId));
        Assert.Equal("1060", sdk.GetLobbyData(42, SessionListingDataCodec.AdvertisementExpiresAtKey));

        sdk.UtcNowSeconds = 1_020;
        advertiser.RenewAdvertisementLease();

        Assert.Equal("1080", sdk.GetLobbyData(42, SessionListingDataCodec.AdvertisementExpiresAtKey));
    }

    [Fact]
    public void RenewAdvertisementLease_WriteFailureWithdrawsListing()
    {
        var sdk = new FakeGalaxySdk();
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);
        advertiser.Advertise(CreateInfo(sdk.LocalUserId));
        sdk.FailedLobbyDataKey = SessionListingDataCodec.AdvertisementExpiresAtKey;

        advertiser.RenewAdvertisementLease();

        Assert.False(advertiser.IsAdvertising);
        Assert.Contains(42UL, sdk.LeftLobbies);
        Assert.True(sdk.RichPresenceCleared);

        sdk.FailedLobbyDataKey = string.Empty;
        sdk.NextLobbyId = 43;
        advertiser.RetryCreate();

        Assert.True(advertiser.IsAdvertising);
        Assert.Equal(new SessionListingId("gog", "43"), advertiser.ListingId);
    }

    [Fact]
    public void RenewAdvertisementLease_AsynchronousFailureWithdrawsListing()
    {
        var sdk = new FakeGalaxySdk { CompleteLobbyDataWritesImmediately = false };
        using var advertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false);
        advertiser.Advertise(CreateInfo(sdk.LocalUserId));
        sdk.CompleteAllLobbyDataWrites();
        sdk.FailedLobbyDataKey = SessionListingDataCodec.AdvertisementExpiresAtKey;

        advertiser.RenewAdvertisementLease();
        Assert.True(advertiser.IsAdvertising);

        sdk.CompleteAllLobbyDataWrites();

        Assert.False(advertiser.IsAdvertising);
        Assert.Contains(42UL, sdk.LeftLobbies);
    }

    [Fact]
    public void InviteFriends_UsesOwnedOrJoinedGogLobby()
    {
        var sdk = new FakeGalaxySdk();
        using (var ownerAdvertiser = new GalaxyLobbyAdvertiser(
            sdk,
            new FakeMembership(),
            ServerVisibility.Public,
            dedicatedServer: false))
        {
            ownerAdvertiser.Advertise(CreateInfo(sdk.LocalUserId));
            Assert.True(ownerAdvertiser.InviteFriends());
        }

        var membership = new FakeMembership
        {
            IsInSession = true,
            ListingId = new SessionListingId("gog", "77"),
        };
        using var guestAdvertiser = new GalaxyLobbyAdvertiser(
            sdk,
            membership,
            ServerVisibility.Public,
            dedicatedServer: false);
        Assert.True(guestAdvertiser.InviteFriends());

        Assert.Contains(GalaxyLobbyAdvertiser.BuildConnectString(42), sdk.InviteDialogs);
        Assert.Contains(GalaxyLobbyAdvertiser.BuildConnectString(77), sdk.InviteDialogs);
    }

    private static SessionJoinInfo CreateInfo(ulong tunnelUserId) => new SessionJoinInfo
    {
        Port = 4200,
        TunnelTarget = new PlatformIdentity("gog", tunnelUserId.ToString()),
        ModVersion = ModInformation.BuildVersion,
    };

    private sealed class FakeMembership : ISessionMembership
    {
        public bool IsInSession { get; set; }
        public SessionListingId ListingId { get; set; }
        public void JoinSession(SessionListingId listingId) => ListingId = listingId;
        public void LeaveSession() => ListingId = default;
    }
}
