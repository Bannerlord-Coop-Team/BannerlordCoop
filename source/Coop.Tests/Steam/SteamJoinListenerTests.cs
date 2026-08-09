using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Coop.Steam;
using Coop.Tests.Stubs;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Steam
{
    public class SteamJoinListenerTests
    {
        private readonly FakeSteamLobbyApi api = new FakeSteamLobbyApi();
        private readonly StubMessageBroker messageBroker = new StubMessageBroker();
        private readonly FakeSessionJoinRequestGate joinRequestGate = new FakeSessionJoinRequestGate();
        private readonly SteamJoinListener listener;

        private readonly List<SessionJoinInfoResolved> resolved = new List<SessionJoinInfoResolved>();
        private readonly List<SessionJoinFailed> failed = new List<SessionJoinFailed>();

        public SteamJoinListenerTests()
        {
            listener = new SteamJoinListener(messageBroker, api, joinRequestGate);

            messageBroker.Subscribe<SessionJoinInfoResolved>(Handle_Resolved);
            messageBroker.Subscribe<SessionJoinFailed>(Handle_Failed);
        }

        private void Handle_Resolved(MessagePayload<SessionJoinInfoResolved> payload) => resolved.Add(payload.What);
        private void Handle_Failed(MessagePayload<SessionJoinFailed> payload) => failed.Add(payload.What);

        private void SetupLobby(ulong lobbyId, string address = "203.0.113.7", int port = 4200,
            int version = SessionJoinInfo.CurrentVersion, ulong tunnelSteamId = 0)
        {
            var info = new SessionJoinInfo
            {
                Address = address,
                Port = port,
                Version = version,
                TunnelTarget = SteamIdentity(tunnelSteamId),
                ModVersion = Common.ModInformation.BuildVersion,
            };

            foreach (var pair in SessionListingDataCodec.Encode(info))
            {
                api.SetLobbyData(lobbyId, pair.Key, pair.Value);
            }
        }

        private static PlatformIdentity SteamIdentity(ulong steamId) => steamId == 0
            ? default
            : new PlatformIdentity(SteamSessionProvider.ProviderId, steamId.ToString());

        [Fact]
        public void LobbyJoinRequest_PublishesResolvedJoinInfoAndRetainsMembership()
        {
            SetupLobby(42);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal("203.0.113.7", info.Address);
            Assert.Equal(4200, info.Port);
            Assert.Empty(failed);
            Assert.True(listener.IsInSession);
            Assert.Equal(new SessionListingId("steam", "42"), listener.ListingId);
            Assert.DoesNotContain(42UL, api.LeftLobbies);
        }

        [Fact]
        public void JoinSessionListingMessage_PublishesResolvedJoinInfo()
        {
            SetupLobby(42);

            messageBroker.Publish(this, new JoinSessionListing(new SessionListingId("steam", "42")));

            Assert.Single(resolved);
        }

        [Fact]
        public void JoinSession_RetainsMembershipWithoutResolvingAgain()
        {
            listener.JoinSession(new SessionListingId("steam", "42"));

            Assert.True(listener.IsInSession);
            Assert.Equal(new SessionListingId("steam", "42"), listener.ListingId);
            Assert.Empty(resolved);
            Assert.Empty(failed);
        }

        [Fact]
        public void LeaveSession_LeavesActiveLobby()
        {
            listener.JoinSession(new SessionListingId("steam", "42"));

            listener.LeaveSession();

            Assert.False(listener.IsInSession);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void LeaveSession_WhileJoinInFlight_LeavesLateLobby()
        {
            api.CompleteOperationsImmediately = false;
            listener.JoinSession(new SessionListingId("steam", "42"));

            listener.LeaveSession();
            api.CompletePendingJoin();

            Assert.False(listener.IsInSession);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void AbandonedJoin_LeavesLobbyAndAllowsRejoin()
        {
            SetupLobby(42);
            api.RaiseLobbyJoinRequested(42);

            // Canceling the password prompt abandons the attempt without a session.
            messageBroker.Publish(this, new SessionJoinAbandoned());

            Assert.False(listener.IsInSession);
            Assert.Contains(42UL, api.LeftLobbies);

            api.RaiseLobbyJoinRequested(42);

            Assert.Equal(2, resolved.Count);
            Assert.True(listener.IsInSession);
            Assert.Empty(failed);
        }

        [Fact]
        public void RejoinRequestWhileStillMember_ResolvesAgain()
        {
            SetupLobby(42);

            api.RaiseLobbyJoinRequested(42);
            api.RaiseLobbyJoinRequested(42);

            Assert.Equal(2, resolved.Count);
            Assert.Empty(failed);
            Assert.True(listener.IsInSession);
            Assert.DoesNotContain(42UL, api.LeftLobbies);
        }

        [Fact]
        public void RejoinRequestWhileStillMember_ReadThrows_LeavesLobbyAndReportsFailure()
        {
            SetupLobby(42);
            api.RaiseLobbyJoinRequested(42);
            Assert.True(listener.IsInSession);

            // A lobby read throwing on the re-resolve must not strand the join in the lobby:
            // release the membership and surface the failure, same as the OnLobbyEntered path.
            api.ThrowOnGetLobbyData = true;
            api.RaiseLobbyJoinRequested(42);

            Assert.False(listener.IsInSession);
            Assert.Contains(42UL, api.LeftLobbies);
            Assert.Single(failed);
        }

        [Fact]
        public void ConnectString_JoinsReferencedLobby()
        {
            SetupLobby(42);

            api.RaiseConnectStringReceived("+connect_lobby 42");

            Assert.Single(resolved);
        }

        [Fact]
        public void LobbyJoinRequest_WhenSessionRejectsJoin_KeepsCurrentLobbyMembership()
        {
            listener.JoinSession(new SessionListingId("steam", "41"));
            joinRequestGate.CanStart = false;
            api.CompleteOperationsImmediately = false;

            api.RaiseLobbyJoinRequested(42);

            Assert.Equal(new SessionListingId("steam", "41"), listener.ListingId);
            Assert.DoesNotContain(41UL, api.LeftLobbies);
            Assert.Null(api.PendingJoinCompletion);
            Assert.Empty(resolved);
        }

        [Fact]
        public void LaunchArguments_JoinLobbyFromSteamLaunchCommandLine()
        {
            SetupLobby(42);
            api.LaunchCommandLine = "+connect_lobby 42";

            listener.ProcessLaunchArguments("Bannerlord.exe /singleplayer /client");

            Assert.Single(resolved);
        }

        [Fact]
        public void OverlappingJoinRequests_OnlyFirstProceeds()
        {
            SetupLobby(42);
            SetupLobby(43, address: "198.51.100.9");
            api.CompleteOperationsImmediately = false;

            api.RaiseLobbyJoinRequested(42);
            api.RaiseLobbyJoinRequested(43);
            api.CompletePendingJoin();

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal("203.0.113.7", info.Address);

            api.RaiseLobbyJoinRequested(43);
            api.CompletePendingJoin();

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void FailedLobbyJoin_PublishesFailure()
        {
            api.JoinSucceeds = false;

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Single(failed);
        }

        [Fact]
        public void NonCoopLobby_PublishesFailureAndLeaves()
        {
            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Single(failed);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void DifferentModVersion_PublishesFailureAndLeaves()
        {
            SetupLobby(42);
            api.SetLobbyData(42, SessionListingDataCodec.ModVersionKey, "different-build");

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Contains("mod", Assert.Single(failed).Reason);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void DirectOnlyLobbyWithoutAddress_PublishesFailureAndLeaves()
        {
            SetupLobby(42, address: null);

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Contains("public address", Assert.Single(failed).Reason);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void SteamTunnelLobby_ResolvesAdvertisedIdentityWithoutAddress()
        {
            SetupLobby(42, address: null, tunnelSteamId: api.LobbyOwner);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal(SteamIdentity(api.LobbyOwner), info.TunnelTarget);
            Assert.False(info.HasAddress);
            Assert.Empty(failed);
            Assert.True(listener.IsInSession);
            Assert.DoesNotContain(42UL, api.LeftLobbies);
        }

        [Fact]
        public void StandaloneServerLobby_UsesAdvertisedTunnelIdentity()
        {
            SetupLobby(42, address: null, tunnelSteamId: 76561198000000042);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal(SteamIdentity(76561198000000042), info.TunnelTarget);
            Assert.NotEqual(SteamIdentity(api.LobbyOwner), info.TunnelTarget);
            Assert.Empty(failed);
        }

        [Fact]
        public void DifferentProviderTunnelIdentity_IsRejected()
        {
            SetupLobby(42, address: null);
            api.SetLobbyData(42, SessionListingDataCodec.TunnelProviderKey, "gog");
            api.SetLobbyData(42, SessionListingDataCodec.TunnelPeerIdKey, "76561198000000042");

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Contains("different networking provider", Assert.Single(failed).Reason);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void OwnLobby_LeaveSessionKeepsSteamMembership()
        {
            SetupLobby(42, address: null, tunnelSteamId: 76561198000000042);
            api.UserSteamId = api.LobbyOwner;

            api.RaiseLobbyJoinRequested(42);
            listener.LeaveSession();

            Assert.Single(resolved);
            Assert.Empty(failed);
            Assert.False(listener.IsInSession);
            // Leaving as the lobby's owning account would empty the lobby and Steam would
            // destroy it, delisting the dedicated server this client just connected to.
            Assert.Empty(api.LeftLobbies);
        }

        [Fact]
        public void OwnLobby_StaysEvenWhenJoinInfoIsRejected()
        {
            api.UserSteamId = api.LobbyOwner;

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Single(failed);
            Assert.False(listener.IsInSession);
            Assert.Empty(api.LeftLobbies);
        }

        [Fact]
        public void PromotedToLobbyOwnerAfterAdvertiserLeft_LeaveSessionStillLeaves()
        {
            SetupLobby(42);
            api.RaiseLobbyJoinRequested(42);

            // Server shutdown disconnects clients before withdrawing the lobby; Steam
            // promotes this client to owner before its teardown runs on the game thread.
            api.LobbyOwner = api.UserSteamId;
            listener.LeaveSession();

            Assert.False(listener.IsInSession);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void DirectOnlyLobby_ResolvesWithoutTunnelIdentity()
        {
            SetupLobby(42);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.False(info.HasTunnelTarget);
            Assert.Equal("203.0.113.7", info.Address);
        }

        [Theory]
        [InlineData(null, false, 0ul)]
        [InlineData("", false, 0ul)]
        [InlineData("/singleplayer /client", false, 0ul)]
        [InlineData("+connect_lobby", false, 0ul)]
        [InlineData("+connect_lobby abc", false, 0ul)]
        [InlineData("+connect_lobby 42", true, 42ul)]
        [InlineData("Bannerlord.exe /client +connect_lobby 42 /other", true, 42ul)]
        public void TryParseConnectLobby_ParsesArguments(string text, bool expectedResult, ulong expectedLobbyId)
        {
            Assert.Equal(expectedResult, SteamJoinListener.TryParseConnectLobby(text, out var lobbyId));
            Assert.Equal(expectedLobbyId, lobbyId);
        }

        private sealed class FakeSessionJoinRequestGate : ISessionJoinRequestGate
        {
            public bool CanStart { get; set; } = true;
            public bool CanStartJoin() => CanStart;
        }
    }
}
