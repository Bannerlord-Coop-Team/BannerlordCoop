using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Coop.GOG;
using Coop.Tests.Stubs;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.GOG;

public class GalaxyJoinListenerTests : IDisposable
{
    private readonly FakeGalaxySdk sdk = new FakeGalaxySdk();
    private readonly StubMessageBroker messageBroker = new StubMessageBroker();
    private readonly FakeSessionJoinRequestGate joinRequestGate = new FakeSessionJoinRequestGate();
    private readonly GalaxyJoinListener listener;
    private readonly List<SessionJoinInfoResolved> resolved = new List<SessionJoinInfoResolved>();
    private readonly List<SessionJoinFailed> failed = new List<SessionJoinFailed>();

    public GalaxyJoinListenerTests()
    {
        listener = new GalaxyJoinListener(messageBroker, sdk, joinRequestGate);
        messageBroker.Subscribe<SessionJoinInfoResolved>(payload => resolved.Add(payload.What));
        messageBroker.Subscribe<SessionJoinFailed>(payload => failed.Add(payload.What));
    }

    public void Dispose() => listener.Dispose();

    [Fact]
    public void ListingJoin_PublishesResolvedInfoAndRetainsMembership()
    {
        GalaxyLobbyBrowserTests.ConfigureListing(sdk, 42, ownerId: 500, ownerName: "host");

        messageBroker.Publish(this, new JoinSessionListing(new SessionListingId("gog", "42")));

        var info = Assert.Single(resolved).JoinInfo;
        Assert.Equal(new PlatformIdentity("gog", "500"), info.TunnelTarget);
        Assert.False(info.HasAddress);
        Assert.True(listener.IsInSession);
        Assert.Equal(new SessionListingId("gog", "42"), listener.ListingId);
        Assert.Empty(failed);
    }

    [Fact]
    public void JoinSession_MembershipOnlyDoesNotResolveAgain()
    {
        listener.JoinSession(new SessionListingId("gog", "42"));

        Assert.True(listener.IsInSession);
        Assert.Empty(resolved);
        Assert.Empty(failed);
    }

    [Fact]
    public void GameJoinRequest_ParsesGalaxyConnectArgument()
    {
        GalaxyLobbyBrowserTests.ConfigureListing(sdk, 42, ownerId: 500, ownerName: "host");

        sdk.RaiseGameJoinRequested("Bannerlord.exe /client +connect_gog_lobby 42 /singleplayer");

        Assert.Single(resolved);
        Assert.Equal(42UL, Assert.Single(sdk.JoinRequests));
    }

    [Fact]
    public void GameJoinRequest_WhenSessionRejectsJoin_KeepsCurrentLobbyMembership()
    {
        listener.JoinSession(new SessionListingId("gog", "41"));
        joinRequestGate.CanStart = false;

        sdk.RaiseGameJoinRequested("+connect_gog_lobby 42");

        Assert.Equal(new SessionListingId("gog", "41"), listener.ListingId);
        Assert.Equal(41UL, Assert.Single(sdk.JoinRequests));
        Assert.Empty(sdk.LeftLobbies);
        Assert.Empty(resolved);
    }

    [Fact]
    public void MismatchedAdvertisedIdentityIsRejectedAndLobbyIsLeft()
    {
        GalaxyLobbyBrowserTests.ConfigureListing(sdk, 42, ownerId: 500, ownerName: "host");
        sdk.SetLobbyData(42, SessionListingDataCodec.TunnelPeerIdKey, "999");

        messageBroker.Publish(this, new JoinSessionListing(new SessionListingId("gog", "42")));

        Assert.Empty(resolved);
        Assert.Contains("does not match", Assert.Single(failed).Reason);
        Assert.Contains(42UL, sdk.LeftLobbies);
        Assert.False(listener.IsInSession);
    }

    [Fact]
    public void DifferentStorefrontListingIsIgnored()
    {
        messageBroker.Publish(this, new JoinSessionListing(new SessionListingId("steam", "42")));

        Assert.Empty(sdk.JoinRequests);
        Assert.Empty(resolved);
        Assert.Empty(failed);
    }

    [Fact]
    public void AbandonedInFlightJoinLeavesLateLobby()
    {
        sdk.CompleteJoinImmediately = false;
        sdk.SetLobbyOwner(42, 500);
        listener.JoinSession(new SessionListingId("gog", "42"));

        messageBroker.Publish(this, new SessionJoinAbandoned());
        sdk.CompleteJoin();

        Assert.False(listener.IsInSession);
        Assert.Contains(42UL, sdk.LeftLobbies);
    }

    [Fact]
    public void LeaveOwnLobbyKeepsSdkMembershipNeededByAdvertisement()
    {
        GalaxyLobbyBrowserTests.ConfigureListing(
            sdk,
            42,
            ownerId: sdk.LocalUserId,
            ownerName: "self");
        messageBroker.Publish(this, new JoinSessionListing(new SessionListingId("gog", "42")));

        listener.LeaveSession();

        Assert.False(listener.IsInSession);
        Assert.DoesNotContain(42UL, sdk.LeftLobbies);
    }

    [Theory]
    [InlineData(null, false, 0ul)]
    [InlineData("", false, 0ul)]
    [InlineData("+connect_gog_lobby", false, 0ul)]
    [InlineData("+connect_gog_lobby abc", false, 0ul)]
    [InlineData("+connect_gog_lobby 0", false, 0ul)]
    [InlineData("+connect_gog_lobby 42", true, 42ul)]
    [InlineData("game.exe +CONNECT_GOG_LOBBY 42 /client", true, 42ul)]
    public void TryParseConnectLobby_ValidatesConnectionString(
        string text,
        bool expected,
        ulong expectedLobbyId)
    {
        Assert.Equal(expected, GalaxyJoinListener.TryParseConnectLobby(text, out ulong lobbyId));
        Assert.Equal(expectedLobbyId, lobbyId);
    }

    private sealed class FakeSessionJoinRequestGate : ISessionJoinRequestGate
    {
        public bool CanStart { get; set; } = true;
        public bool CanStartJoin() => CanStart;
    }
}
