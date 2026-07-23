using Common.Network.Session;
using Coop.Steam;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamLobbyAdvertiserTests
    {
        private readonly FakeSteamLobbyApi api = new FakeSteamLobbyApi();
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
                ServerSteamId = 76561198000000042,
            };

        [Fact]
        public void Advertise_CreatesLobbyWithDataAndRichPresence()
        {
            advertiser.Advertise(Info());

            Assert.True(advertiser.IsAdvertising);
            Assert.False(api.LastCreateWasPublic);
            Assert.Equal("203.0.113.7", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.AddressKey));
            Assert.Equal("4200", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.PortKey));
            Assert.Equal(api.PersonaName,
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.OwnerNameKey));
            Assert.Contains($"{SteamLobbyAdvertiser.ConnectLobbyArgument} {api.NextCreatedLobbyId}", api.RichPresenceConnects);
        }

        [Fact]
        public void PublicAdvertiser_CreatesBrowsableLobby()
        {
            var publicAdvertiser = new SteamPublicLobbyAdvertiser(api);

            publicAdvertiser.Advertise(StandaloneInfo());

            Assert.True(publicAdvertiser.IsAdvertising);
            Assert.True(api.LastCreateWasPublic);
            Assert.Equal("public",
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.VisibilityKey));
            Assert.Equal(LobbyDataCodec.StandaloneLobbyType,
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.LobbyTypeKey));
        }

        [Fact]
        public void StandaloneAdvertiser_FriendsOnly_CreatesFriendsOnlyLobby()
        {
            var friendsOnlyAdvertiser = new SteamPublicLobbyAdvertiser(api, ServerVisibility.FriendsOnly);

            friendsOnlyAdvertiser.Advertise(StandaloneInfo());

            Assert.True(friendsOnlyAdvertiser.IsAdvertising);
            Assert.False(api.LastCreateWasPublic);
            Assert.Equal("203.0.113.7",
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.AddressKey));
            Assert.Equal("friends_only",
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.VisibilityKey));
        }

        [Fact]
        public void StandaloneAdvertiser_None_CreatesUnlistedSteamLobbyAndRichPresence()
        {
            var hiddenAdvertiser = new SteamPublicLobbyAdvertiser(api, ServerVisibility.None);

            hiddenAdvertiser.Advertise(StandaloneInfo());

            Assert.True(hiddenAdvertiser.IsAdvertising);
            Assert.True(api.LastCreateWasPublic);
            Assert.Null(api.PendingCreateCompletion);
            Assert.Equal("none",
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.VisibilityKey));
            Assert.Equal("203.0.113.7",
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.AddressKey));
            Assert.Equal(LobbyDataCodec.HiddenStandaloneLobbyType,
                api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.LobbyTypeKey));
            Assert.Contains($"{SteamLobbyAdvertiser.ConnectLobbyArgument} {api.NextCreatedLobbyId}",
                api.RichPresenceConnects);
        }

        [Fact]
        public void StandaloneAdvertiser_RejectsUnknownVisibility()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new SteamPublicLobbyAdvertiser(api, (ServerVisibility)999));
        }

        [Fact]
        public void Advertise_Again_UpdatesDataWithoutSecondLobby()
        {
            advertiser.Advertise(Info());
            var updatedInfo = Info(address: "198.51.100.9");
            updatedInfo.ConnectedPlayers = 3;
            advertiser.Advertise(updatedInfo);

            Assert.Null(api.PendingCreateCompletion);
            Assert.Equal("198.51.100.9", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.AddressKey));
            Assert.Equal("3", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.ConnectedPlayersKey));
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
                LobbyDataCodec.ConnectedPlayersKey));
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
            api.FailedLobbyDataKey = LobbyDataCodec.OwnerNameKey;

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
            var membership = new StubSteamLobbyMembership { LobbyId = 42 };
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

        private sealed class StubSteamLobbyMembership : ISteamLobbyMembership
        {
            public ulong LobbyId { get; set; }
            public bool IsInLobby => LobbyId != 0;

            public void JoinSessionLobby(ulong lobbyId) => LobbyId = lobbyId;
            public void LeaveSessionLobby() => LobbyId = 0;
        }
    }
}
