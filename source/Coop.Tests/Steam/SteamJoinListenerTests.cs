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
        private readonly SteamJoinListener listener;

        private readonly List<SessionJoinInfoResolved> resolved = new List<SessionJoinInfoResolved>();
        private readonly List<SessionJoinFailed> failed = new List<SessionJoinFailed>();

        public SteamJoinListenerTests()
        {
            listener = new SteamJoinListener(messageBroker, api);

            messageBroker.Subscribe<SessionJoinInfoResolved>(Handle_Resolved);
            messageBroker.Subscribe<SessionJoinFailed>(Handle_Failed);
        }

        private void Handle_Resolved(MessagePayload<SessionJoinInfoResolved> payload) => resolved.Add(payload.What);
        private void Handle_Failed(MessagePayload<SessionJoinFailed> payload) => failed.Add(payload.What);

        private void SetupLobby(ulong lobbyId, string address = "203.0.113.7", int port = 4200,
            int version = SessionJoinInfo.CurrentVersion, ulong serverSteamId = 0)
        {
            var info = new SessionJoinInfo
            {
                Address = address,
                Port = port,
                Version = version,
                ServerSteamId = serverSteamId,
                ModVersion = Common.ModInformation.BuildVersion,
            };

            foreach (var pair in LobbyDataCodec.Encode(info))
            {
                api.SetLobbyData(lobbyId, pair.Key, pair.Value);
            }
        }

        [Fact]
        public void LobbyJoinRequest_PublishesResolvedJoinInfoAndRetainsMembership()
        {
            SetupLobby(42);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal("203.0.113.7", info.Address);
            Assert.Equal(4200, info.Port);
            Assert.Empty(failed);
            Assert.True(listener.IsInLobby);
            Assert.Equal(42UL, listener.LobbyId);
            Assert.DoesNotContain(42UL, api.LeftLobbies);
        }

        [Fact]
        public void JoinSteamLobbyMessage_PublishesResolvedJoinInfo()
        {
            SetupLobby(42);

            messageBroker.Publish(this, new JoinSteamLobby(42));

            Assert.Single(resolved);
        }

        [Fact]
        public void JoinSessionLobby_RetainsMembershipWithoutResolvingAgain()
        {
            listener.JoinSessionLobby(42);

            Assert.True(listener.IsInLobby);
            Assert.Equal(42UL, listener.LobbyId);
            Assert.Empty(resolved);
            Assert.Empty(failed);
        }

        [Fact]
        public void LeaveSessionLobby_LeavesActiveLobby()
        {
            listener.JoinSessionLobby(42);

            listener.LeaveSessionLobby();

            Assert.False(listener.IsInLobby);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void LeaveSessionLobby_WhileJoinInFlight_LeavesLateLobby()
        {
            api.CompleteOperationsImmediately = false;
            listener.JoinSessionLobby(42);

            listener.LeaveSessionLobby();
            api.CompletePendingJoin();

            Assert.False(listener.IsInLobby);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void ConnectString_JoinsReferencedLobby()
        {
            SetupLobby(42);

            api.RaiseConnectStringReceived("+connect_lobby 42");

            Assert.Single(resolved);
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
            api.SetLobbyData(42, LobbyDataCodec.ModVersionKey, "different-build");

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Contains("mod", Assert.Single(failed).Reason);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void DirectOnlyLobbyWithoutAddress_PublishesFailureAndLeaves()
        {
            SetupLobby(42, address: null, version: SessionJoinInfo.MinTunnelVersion - 1);

            api.RaiseLobbyJoinRequested(42);

            Assert.Empty(resolved);
            Assert.Contains("public address", Assert.Single(failed).Reason);
            Assert.Contains(42UL, api.LeftLobbies);
        }

        [Fact]
        public void TunnelCapableLobby_ResolvesHostSteamIdWithoutAddress()
        {
            SetupLobby(42, address: null);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal(api.LobbyOwner, info.HostSteamId);
            Assert.False(info.HasAddress);
            Assert.Empty(failed);
            Assert.True(listener.IsInLobby);
            Assert.DoesNotContain(42UL, api.LeftLobbies);
        }

        [Fact]
        public void StandaloneServerLobby_PrefersServerSteamIdOverLobbyOwner()
        {
            SetupLobby(42, address: null, serverSteamId: 76561198000000042);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.Equal(76561198000000042UL, info.HostSteamId);
            Assert.NotEqual(api.LobbyOwner, info.HostSteamId);
            Assert.Empty(failed);
        }

        [Fact]
        public void OwnLobby_LeaveSessionLobbyKeepsSteamMembership()
        {
            SetupLobby(42, address: null, serverSteamId: 76561198000000042);
            api.UserSteamId = api.LobbyOwner;

            api.RaiseLobbyJoinRequested(42);
            listener.LeaveSessionLobby();

            Assert.Single(resolved);
            Assert.Empty(failed);
            Assert.False(listener.IsInLobby);
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
            Assert.False(listener.IsInLobby);
            Assert.Empty(api.LeftLobbies);
        }

        [Fact]
        public void DirectOnlyLobby_ResolvesWithoutHostSteamId()
        {
            SetupLobby(42, version: SessionJoinInfo.MinTunnelVersion - 1);

            api.RaiseLobbyJoinRequested(42);

            var info = Assert.Single(resolved).JoinInfo;
            Assert.False(info.HasHostSteamId);
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
    }
}
