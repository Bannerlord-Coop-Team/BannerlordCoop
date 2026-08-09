using Common.Network.Session;
using Coop.Steam;
using System;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamLobbyAdvertiserTests
    {
        private readonly FakeSteamLobbyApi api = new FakeSteamLobbyApi();
        private readonly FakeSteamLobbyLeaseRenewer leaseRenewer = new FakeSteamLobbyLeaseRenewer();
        private readonly SteamLobbyAdvertiser advertiser;

        public SteamLobbyAdvertiserTests()
        {
            advertiser = new SteamLobbyAdvertiser(api);
        }

        private static SessionJoinInfo Info(string address = "203.0.113.7", int port = 4200) =>
            new SessionJoinInfo { Address = address, Port = port };

        private static SessionJoinInfo StandaloneInfo(
            string address = "203.0.113.7", int port = 4200) =>
            new SessionJoinInfo
            {
                Address = address,
                Port = port,
                TunnelTarget = SteamIdentity(90100000000000042),
                DedicatedServer = true,
            };

        private static PlatformIdentity SteamIdentity(ulong steamId) =>
            new PlatformIdentity(SteamSessionProvider.ProviderId, steamId.ToString());

        private SteamPublicLobbyAdvertiser CreatePublicAdvertiser(
            ServerVisibility visibility = ServerVisibility.Public)
            => new SteamPublicLobbyAdvertiser(api, visibility, leaseRenewer);

        [Fact]
        public void Advertise_CreatesLobbyWithDataAndRichPresence()
        {
            advertiser.Advertise(Info());

            Assert.True(advertiser.IsAdvertising);
            Assert.False(api.LastCreateWasPublic);
            Assert.Equal("203.0.113.7", api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.AddressKey));
            Assert.Equal("4200", api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.PortKey));
            Assert.Equal(api.PersonaName,
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.OwnerNameKey));
            Assert.Contains($"{SteamLobbyAdvertiser.ConnectLobbyArgument} {api.NextCreatedLobbyId}", api.RichPresenceConnects);
        }

        [Fact]
        public void PublicAdvertiser_CreatesBrowsableLobby()
        {
            var publicAdvertiser = CreatePublicAdvertiser();

            publicAdvertiser.Advertise(StandaloneInfo());

            Assert.True(publicAdvertiser.IsAdvertising);
            Assert.True(api.LastCreateWasPublic);
            Assert.Equal("public",
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.VisibilityKey));
            Assert.Equal(
                SessionListingDataCodec.EncodeAdvertisementExpiry(
                    api.SteamServerTime + SteamPublicLobbyAdvertiser.AdvertisementLeaseSeconds),
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.AdvertisementExpiresAtKey));
            Assert.Equal(SessionListingDataCodec.DedicatedListingType,
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.ListingTypeKey));
            Assert.True(leaseRenewer.IsRunning);
        }

        [Fact]
        public void StandaloneAdvertiser_FriendsOnly_CreatesFriendsOnlyLobby()
        {
            var friendsOnlyAdvertiser = CreatePublicAdvertiser(ServerVisibility.FriendsOnly);

            friendsOnlyAdvertiser.Advertise(StandaloneInfo());

            Assert.True(friendsOnlyAdvertiser.IsAdvertising);
            Assert.False(api.LastCreateWasPublic);
            Assert.Equal("203.0.113.7",
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.AddressKey));
            Assert.Equal("friends_only",
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.VisibilityKey));
        }

        [Fact]
        public void StandaloneAdvertiser_None_CreatesUnlistedSteamLobbyAndRichPresence()
        {
            var hiddenAdvertiser = CreatePublicAdvertiser(ServerVisibility.None);

            hiddenAdvertiser.Advertise(StandaloneInfo());

            Assert.True(hiddenAdvertiser.IsAdvertising);
            Assert.True(api.LastCreateWasPublic);
            Assert.Null(api.PendingCreateCompletion);
            Assert.Equal("none",
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.VisibilityKey));
            Assert.Equal("203.0.113.7",
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.AddressKey));
            Assert.Equal(SessionListingDataCodec.HiddenDedicatedListingType,
                api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.ListingTypeKey));
            Assert.Contains($"{SteamLobbyAdvertiser.ConnectLobbyArgument} {api.NextCreatedLobbyId}",
                api.RichPresenceConnects);
        }

        [Fact]
        public void StandaloneAdvertiser_RejectsUnknownVisibility()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                CreatePublicAdvertiser((ServerVisibility)999));
        }

        [Fact]
        public void PublicAdvertiser_RenewsAdvertisementLease()
        {
            api.SteamServerTime = 1_000;
            var publicAdvertiser = CreatePublicAdvertiser();
            publicAdvertiser.Advertise(StandaloneInfo());

            api.SteamServerTime = 1_020;
            leaseRenewer.Renew();

            Assert.Equal("1080",
                api.GetLobbyData(api.NextCreatedLobbyId,
                    SessionListingDataCodec.AdvertisementExpiresAtKey));
        }

        [Fact]
        public void PublicAdvertiser_UnavailableSteamTimeFailsOpen()
        {
            api.SteamServerTime = 0;
            var publicAdvertiser = CreatePublicAdvertiser();

            publicAdvertiser.Advertise(StandaloneInfo());

            Assert.Equal(uint.MaxValue.ToString(),
                api.GetLobbyData(api.NextCreatedLobbyId,
                    SessionListingDataCodec.AdvertisementExpiresAtKey));
        }

        [Fact]
        public void PublicAdvertiser_UnavailableSteamTimePreservesExistingLease()
        {
            api.SteamServerTime = 1_000;
            var publicAdvertiser = CreatePublicAdvertiser();
            publicAdvertiser.Advertise(StandaloneInfo());
            string initialExpiry = api.GetLobbyData(api.NextCreatedLobbyId,
                SessionListingDataCodec.AdvertisementExpiresAtKey);

            api.SteamServerTime = 0;
            leaseRenewer.Renew();

            Assert.Equal(initialExpiry,
                api.GetLobbyData(api.NextCreatedLobbyId,
                    SessionListingDataCodec.AdvertisementExpiresAtKey));
            Assert.True(publicAdvertiser.IsAdvertising);
        }

        [Fact]
        public void PublicAdvertiser_UnavailableSteamTimePreservesLeaseDuringMetadataUpdate()
        {
            api.SteamServerTime = 1_000;
            var publicAdvertiser = CreatePublicAdvertiser();
            var info = StandaloneInfo();
            publicAdvertiser.Advertise(info);
            string initialExpiry = api.GetLobbyData(api.NextCreatedLobbyId,
                SessionListingDataCodec.AdvertisementExpiresAtKey);

            api.SteamServerTime = 0;
            info.ConnectedPlayers = 3;
            publicAdvertiser.Advertise(info);

            Assert.Equal(initialExpiry,
                api.GetLobbyData(api.NextCreatedLobbyId,
                    SessionListingDataCodec.AdvertisementExpiresAtKey));
            Assert.Equal("3",
                api.GetLobbyData(api.NextCreatedLobbyId,
                    SessionListingDataCodec.ConnectedPlayersKey));
            Assert.True(publicAdvertiser.IsAdvertising);
        }

        [Fact]
        public void PublicAdvertiser_UnavailableTimeMetadataUpdateKeepsRenewalFailureCount()
        {
            var publicAdvertiser = CreatePublicAdvertiser();
            var info = StandaloneInfo();
            publicAdvertiser.Advertise(info);
            api.FailedLobbyDataKey = SessionListingDataCodec.AdvertisementExpiresAtKey;
            leaseRenewer.Renew();
            leaseRenewer.Renew();

            api.SteamServerTime = 0;
            info.ConnectedPlayers = 3;
            publicAdvertiser.Advertise(info);
            api.SteamServerTime = 1_000;
            leaseRenewer.Renew();

            Assert.False(publicAdvertiser.IsAdvertising);
            Assert.False(leaseRenewer.IsRunning);
            Assert.Contains(api.NextCreatedLobbyId, api.LeftLobbies);
            publicAdvertiser.Dispose();
        }

        [Fact]
        public void PublicAdvertiser_RepeatedLeaseRenewalFailuresWithdrawLobby()
        {
            var publicAdvertiser = CreatePublicAdvertiser();
            publicAdvertiser.Advertise(StandaloneInfo());
            api.FailedLobbyDataKey = SessionListingDataCodec.AdvertisementExpiresAtKey;

            leaseRenewer.Renew();
            leaseRenewer.Renew();

            Assert.True(publicAdvertiser.IsAdvertising);
            Assert.True(leaseRenewer.IsRunning);
            Assert.Empty(api.LeftLobbies);

            leaseRenewer.Renew();

            Assert.False(publicAdvertiser.IsAdvertising);
            Assert.False(leaseRenewer.IsRunning);
            Assert.Contains(api.NextCreatedLobbyId, api.LeftLobbies);
            publicAdvertiser.Dispose();
        }

        [Fact]
        public void PublicAdvertiser_SuccessfulLeaseRenewalResetsFailureCount()
        {
            var publicAdvertiser = CreatePublicAdvertiser();
            publicAdvertiser.Advertise(StandaloneInfo());
            api.FailedLobbyDataKey = SessionListingDataCodec.AdvertisementExpiresAtKey;
            leaseRenewer.Renew();

            api.FailedLobbyDataKey = string.Empty;
            leaseRenewer.Renew();
            api.FailedLobbyDataKey = SessionListingDataCodec.AdvertisementExpiresAtKey;
            leaseRenewer.Renew();
            leaseRenewer.Renew();

            Assert.True(publicAdvertiser.IsAdvertising);
            Assert.True(leaseRenewer.IsRunning);
            Assert.Empty(api.LeftLobbies);
        }

        [Fact]
        public void PublicAdvertiser_StopAdvertisingStopsLeaseRenewal()
        {
            api.SteamServerTime = 1_000;
            var publicAdvertiser = CreatePublicAdvertiser();
            publicAdvertiser.Advertise(StandaloneInfo());
            string initialExpiry = api.GetLobbyData(api.NextCreatedLobbyId,
                SessionListingDataCodec.AdvertisementExpiresAtKey);

            publicAdvertiser.StopAdvertising();
            api.SteamServerTime = 1_020;
            leaseRenewer.Renew();

            Assert.False(leaseRenewer.IsRunning);
            Assert.Equal(initialExpiry, api.GetLobbyData(api.NextCreatedLobbyId,
                SessionListingDataCodec.AdvertisementExpiresAtKey));
        }

        [Fact]
        public void Advertise_Again_UpdatesDataWithoutSecondLobby()
        {
            advertiser.Advertise(Info());
            var updatedInfo = Info(address: "198.51.100.9");
            updatedInfo.ConnectedPlayers = 3;
            advertiser.Advertise(updatedInfo);

            Assert.Null(api.PendingCreateCompletion);
            Assert.Equal("198.51.100.9", api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.AddressKey));
            Assert.Equal("3", api.GetLobbyData(api.NextCreatedLobbyId, SessionListingDataCodec.ConnectedPlayersKey));
            Assert.Empty(api.LeftLobbies);
        }

        [Fact]
        public void Advertise_WhileCreationInFlight_UsesLatestConnectedPlayerCount()
        {
            api.CompleteOperationsImmediately = false;
            var initialInfo = Info();
            initialInfo.ConnectedPlayers = 1;
            var updatedInfo = Info();
            updatedInfo.ConnectedPlayers = 2;

            advertiser.Advertise(initialInfo);
            advertiser.Advertise(updatedInfo);
            api.CompletePendingCreate();

            Assert.Equal("2", api.GetLobbyData(api.NextCreatedLobbyId,
                SessionListingDataCodec.ConnectedPlayersKey));
        }

        [Fact]
        public void Advertise_FailedCreation_DoesNotAdvertise()
        {
            api.CreateSucceeds = false;

            advertiser.Advertise(Info());

            Assert.False(advertiser.IsAdvertising);
            Assert.Empty(api.RichPresenceConnects);
        }

        [Fact]
        public void Advertise_FailedDataWrite_WithdrawsLobby()
        {
            api.SetLobbyDataSucceeds = false;

            advertiser.Advertise(Info());

            Assert.False(advertiser.IsAdvertising);
            Assert.Contains(api.NextCreatedLobbyId, api.LeftLobbies);
            Assert.Empty(api.RichPresenceConnects);
        }

        [Fact]
        public void Advertise_FailedOwnerNameWriteKeepsJoinableLobby()
        {
            api.FailedLobbyDataKey = SessionListingDataCodec.OwnerNameKey;

            advertiser.Advertise(Info());

            Assert.True(advertiser.IsAdvertising);
            Assert.Empty(api.LeftLobbies);
            Assert.Contains($"{SteamLobbyAdvertiser.ConnectLobbyArgument} {api.NextCreatedLobbyId}",
                api.RichPresenceConnects);
        }

        [Fact]
        public void StopAdvertising_LeavesLobbyAndClearsRichPresence()
        {
            advertiser.Advertise(Info());

            advertiser.StopAdvertising();

            Assert.False(advertiser.IsAdvertising);
            Assert.Contains(api.NextCreatedLobbyId, api.LeftLobbies);
            Assert.Equal(1, api.ClearRichPresenceCalls);
        }

        [Fact]
        public void StopAdvertising_WhileCreationInFlight_LeavesLateLobby()
        {
            api.CompleteOperationsImmediately = false;

            advertiser.Advertise(Info());
            advertiser.StopAdvertising();
            api.CompletePendingCreate();

            Assert.False(advertiser.IsAdvertising);
            Assert.Contains(api.NextCreatedLobbyId, api.LeftLobbies);
        }

        [Fact]
        public void InviteFriends_OpensOverlayDialog()
        {
            advertiser.Advertise(Info());

            Assert.True(advertiser.CanInviteFriends);
            Assert.True(advertiser.InviteFriends());
            Assert.Contains(api.NextCreatedLobbyId, api.InviteDialogsOpened);
        }

        [Fact]
        public void Advertise_RaisesLobbyChangedAfterCreation()
        {
            ulong changedLobbyId = 0;
            advertiser.LobbyChanged += lobbyId => changedLobbyId = lobbyId;

            advertiser.Advertise(Info());

            Assert.Equal(api.NextCreatedLobbyId, changedLobbyId);
        }

        [Fact]
        public void StopAdvertising_RaisesLobbyChangedWithZero()
        {
            ulong changedLobbyId = 1;
            advertiser.LobbyChanged += lobbyId => changedLobbyId = lobbyId;
            advertiser.Advertise(Info());

            advertiser.StopAdvertising();

            Assert.Equal(0UL, changedLobbyId);
        }

        [Fact]
        public void InviteFriends_AsLobbyMember_OpensOverlayDialog()
        {
            var membership = new StubSessionMembership
            {
                ListingId = new SessionListingId(SteamSessionProvider.ProviderId, "42"),
            };
            var memberAdvertiser = new SteamLobbyAdvertiser(api, membership);

            Assert.True(memberAdvertiser.CanInviteFriends);
            Assert.True(memberAdvertiser.InviteFriends());
            Assert.Contains(42UL, api.InviteDialogsOpened);
        }

        [Fact]
        public void InviteFriends_WithoutOverlay_ReturnsFalse()
        {
            api.OverlayEnabled = false;
            advertiser.Advertise(Info());

            Assert.False(advertiser.InviteFriends());
            Assert.Empty(api.InviteDialogsOpened);
        }

        [Fact]
        public void InviteFriends_WithoutLobby_ReturnsFalse()
        {
            Assert.False(advertiser.CanInviteFriends);
            Assert.False(advertiser.InviteFriends());
        }

        [Fact]
        public void Advertise_AfterDispose_DoesNothing()
        {
            advertiser.Dispose();

            advertiser.Advertise(Info());

            Assert.False(advertiser.IsAdvertising);
            Assert.Null(api.PendingCreateCompletion);
            Assert.Empty(api.LobbyData);
            Assert.Equal(0, api.ClearRichPresenceCalls);
        }

        [Fact]
        public void Dispose_WhileCreationInFlight_LeavesLateLobby()
        {
            api.CompleteOperationsImmediately = false;

            advertiser.Advertise(Info());
            advertiser.Dispose();
            api.CompletePendingCreate();

            Assert.False(advertiser.IsAdvertising);
            Assert.Contains(api.NextCreatedLobbyId, api.LeftLobbies);
        }

        private sealed class StubSessionMembership : ISessionMembership
        {
            public SessionListingId ListingId { get; set; }
            public bool IsInSession => ListingId.IsValid;

            public void JoinSession(SessionListingId listingId) => ListingId = listingId;
            public void LeaveSession() => ListingId = default;
        }

        private sealed class FakeSteamLobbyLeaseRenewer : ISteamLobbyLeaseRenewer
        {
            private Action? renew;

            public bool IsRunning => renew != null;

            public void Start(Action renew) => this.renew = renew;
            public void Stop() => renew = null;
            public void Renew() => renew?.Invoke();
            public void Dispose() => Stop();
        }
    }
}
