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

        [Fact]
        public void Advertise_CreatesLobbyWithDataAndRichPresence()
        {
            advertiser.Advertise(Info());

            Assert.True(advertiser.IsAdvertising);
            Assert.Equal("203.0.113.7", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.AddressKey));
            Assert.Equal("4200", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.PortKey));
            Assert.Contains($"{SteamLobbyAdvertiser.ConnectLobbyArgument} {api.NextCreatedLobbyId}", api.RichPresenceConnects);
        }

        [Fact]
        public void Advertise_Again_UpdatesDataWithoutSecondLobby()
        {
            advertiser.Advertise(Info());
            advertiser.Advertise(Info(address: "198.51.100.9"));

            Assert.Null(api.PendingCreateCompletion);
            Assert.Equal("198.51.100.9", api.GetLobbyData(api.NextCreatedLobbyId, LobbyDataCodec.AddressKey));
            Assert.Empty(api.LeftLobbies);
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

            Assert.True(advertiser.InviteFriends());
            Assert.Contains(api.NextCreatedLobbyId, api.InviteDialogsOpened);
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
    }
}
