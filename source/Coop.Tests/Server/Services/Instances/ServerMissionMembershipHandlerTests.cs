using Common;
using Common.Network.Session;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

public class ServerMissionMembershipHandlerTests
{
    private const string InstanceId = "battle-1";
    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    [Fact]
    public void MissionEntered_FansOutProviderAuthenticatedPeerIdentitiesWithoutCrossPlatformCollision()
    {
        var newcomerEndpoint = new IPEndPoint(IPAddress.Loopback, 51001);
        var existingEndpoint = new IPEndPoint(IPAddress.Loopback, 51002);
        var newcomer = CreatePeer(newcomerEndpoint, 1);
        var existing = CreatePeer(existingEndpoint, 2);
        var newcomerIdentity = new PlatformIdentity("gog", "9001");
        var existingIdentity = new PlatformIdentity("steam", "9001");

        var identityResolver = new Mock<IAuthenticatedPeerIdentityResolver>();
        MapIdentity(identityResolver, newcomerEndpoint, newcomerIdentity);
        MapIdentity(identityResolver, existingEndpoint, existingIdentity);

        var network = PublishEntry(
            newcomer, "gog:9001", existing, "steam:9001", identityResolver.Object);

        var sentToExisting = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(existing));
        Assert.Equal("gog:9001", sentToExisting.ControllerId);
        Assert.Equal(newcomerIdentity, sentToExisting.PeerIdentity);

        var sentToNewcomer = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(newcomer));
        Assert.Equal("steam:9001", sentToNewcomer.ControllerId);
        Assert.Equal(existingIdentity, sentToNewcomer.PeerIdentity);
    }

    [Fact]
    public void MissionEntered_EndpointIdentityMustMatchConnectionBoundController()
    {
        var newcomerEndpoint = new IPEndPoint(IPAddress.Loopback, 51015);
        var existingEndpoint = new IPEndPoint(IPAddress.Loopback, 51016);
        var newcomer = CreatePeer(newcomerEndpoint, 15);
        var existing = CreatePeer(existingEndpoint, 16);
        var identityResolver = new Mock<IAuthenticatedPeerIdentityResolver>();
        MapIdentity(identityResolver, newcomerEndpoint, new PlatformIdentity("gog", "reused-port"));
        MapIdentity(identityResolver, existingEndpoint, new PlatformIdentity("steam", "9001"));

        var network = PublishEntry(
            newcomer, "gog:9001", existing, "steam:9001", identityResolver.Object);

        var sentToExisting = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(existing));
        Assert.False(sentToExisting.PeerIdentity.IsValid);
    }

    [Fact]
    public void MissionEntered_DirectPeerDoesNotClaimStorefrontMissionIdentity()
    {
        var directEndpoint = new IPEndPoint(IPAddress.Loopback, 51003);
        var tunneledEndpoint = new IPEndPoint(IPAddress.Loopback, 51004);
        var directPeer = CreatePeer(directEndpoint, 3);
        var tunneledPeer = CreatePeer(tunneledEndpoint, 4);
        var tunneledIdentity = new PlatformIdentity("gog", "9004");
        var identityResolver = new Mock<IAuthenticatedPeerIdentityResolver>();
        MapIdentity(identityResolver, tunneledEndpoint, tunneledIdentity);

        var network = PublishEntry(
            directPeer, "local:123", tunneledPeer, "gog:9004", identityResolver.Object);

        var sentToTunneled = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(tunneledPeer));
        Assert.False(sentToTunneled.PeerIdentity.IsValid);

        var sentToDirect = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(directPeer));
        Assert.Equal(tunneledIdentity, sentToDirect.PeerIdentity);
    }

    [Theory]
    [InlineData("127.0.0.1", "local:42")]
    [InlineData("203.0.113.10", "local:42")]
    public void MissionEntered_UnmappedDirectPeerKeepsRelayFallback(string address, string controllerId)
    {
        var newcomer = CreatePeer(new IPEndPoint(IPAddress.Parse(address), 51005), 5);
        var existing = CreatePeer(new IPEndPoint(IPAddress.Parse("203.0.113.11"), 51006), 6);
        var identityResolver = new Mock<IAuthenticatedPeerIdentityResolver>();

        var network = PublishEntry(
            newcomer, controllerId, existing, "local:existing", identityResolver.Object);

        var sentToExisting = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(existing));
        Assert.False(sentToExisting.PeerIdentity.IsValid);
    }

    [Fact]
    public void MissionEntered_PublishesPostMembershipFirstMemberState()
    {
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51007), 7);
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        var playerManager = CreatePlayerManager(peer, "first");
        var entry = new MissionEntryResult(
            "first",
            InstanceId,
            MissionEntryStatus.Entered,
            Array.Empty<(string, NetPeer)>(),
            Array.Empty<MissionDeparture>(),
            isFirstMember: true);
        missionManager
            .Setup(manager => manager.TryEnterMission(
                peer, "first", InstanceId, out entry))
            .Returns(true);
        MissionMemberEntered? entered = null;
        messageBroker.Subscribe<MissionMemberEntered>(payload => entered = payload.What);
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(peer, new NetworkMissionEntered("first", InstanceId));
        DrainGameThread();

        Assert.True(entered.HasValue);
        Assert.True(entered.Value.IsFirstMember);
    }

    [Fact]
    public void MissionEntered_UsesCurrentPeerIdentityInsteadOfPayloadController()
    {
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51008), 8);
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        var playerManager = CreatePlayerManager(peer, "current");
        var entry = new MissionEntryResult(
            "current",
            InstanceId,
            MissionEntryStatus.Entered,
            Array.Empty<(string, NetPeer)>(),
            Array.Empty<MissionDeparture>(),
            isFirstMember: true);
        missionManager
            .Setup(manager => manager.TryEnterMission(peer, "current", InstanceId, out entry))
            .Returns(true);
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(peer, new NetworkMissionEntered("stale", InstanceId));
        DrainGameThread();

        missionManager.Verify(manager => manager.TryEnterMission(peer, "current", InstanceId, out entry), Times.Once);
    }

    [Fact]
    public void MissionEntered_ReconnectPublishesCompletionResetEvent()
    {
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51013), 13);
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        var playerManager = CreatePlayerManager(peer, "current");
        var entry = new MissionEntryResult(
            "current",
            InstanceId,
            MissionEntryStatus.Reconnected,
            Array.Empty<(string, NetPeer)>(),
            Array.Empty<MissionDeparture>(),
            isFirstMember: false);
        missionManager
            .Setup(manager => manager.TryEnterMission(peer, "current", InstanceId, out entry))
            .Returns(true);
        MissionMemberEntered? entered = null;
        messageBroker.Subscribe<MissionMemberEntered>(payload => entered = payload.What);
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(peer, new NetworkMissionEntered("current", InstanceId));
        DrainGameThread();

        Assert.True(entered.HasValue);
        Assert.Equal("current", entered.Value.ControllerId);
        Assert.Equal(InstanceId, entered.Value.InstanceId);
        Assert.False(entered.Value.IsFirstMember);
    }

    [Fact]
    public void MissionEntered_DuplicateDoesNotPublishCompletionResetEvent()
    {
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51014), 14);
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        var playerManager = CreatePlayerManager(peer, "current");
        var entry = new MissionEntryResult(
            "current",
            InstanceId,
            MissionEntryStatus.Unchanged,
            Array.Empty<(string, NetPeer)>(),
            Array.Empty<MissionDeparture>(),
            isFirstMember: false);
        missionManager
            .Setup(manager => manager.TryEnterMission(peer, "current", InstanceId, out entry))
            .Returns(true);
        MissionMemberEntered? entered = null;
        messageBroker.Subscribe<MissionMemberEntered>(payload => entered = payload.What);
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(peer, new NetworkMissionEntered("current", InstanceId));
        DrainGameThread();

        Assert.False(entered.HasValue);
    }

    [Fact]
    public void MissionEntered_RejectsPeerReplacedByReconnect()
    {
        var oldPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51009), 9);
        var currentPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51010), 10);
        var player = new Player("current", string.Empty, string.Empty, string.Empty, string.Empty);
        var playerManager = new Mock<IPlayerManager>();
        var mappedPlayer = player;
        var mappedPeer = currentPeer;
        playerManager.Setup(manager => manager.TryGetPlayer(oldPeer, out mappedPlayer)).Returns(true);
        playerManager.Setup(manager => manager.TryGetPeer("current", out mappedPeer)).Returns(true);
        var missionManager = new Mock<IMissionManager>();
        var messageBroker = new TestMessageBroker();
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(oldPeer, new NetworkMissionEntered("current", InstanceId));

        missionManager.Verify(
            manager => manager.TryEnterMission(
                It.IsAny<NetPeer>(), It.IsAny<string>(), It.IsAny<string>(), out It.Ref<MissionEntryResult>.IsAny),
            Times.Never);
    }

    [Fact]
    public void MissionLeft_PublishesNothingWhenMembershipWasNotRemoved()
    {
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51011), 11);
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        var playerManager = CreatePlayerManager(peer, "current");
        MissionDeparture published = null!;
        MissionMemberDeparted? departed = null;
        messageBroker.Subscribe<MissionMemberDeparted>(payload => departed = payload.What);
        missionManager
            .Setup(manager => manager.TryLeaveMission(peer, "current", InstanceId, out published))
            .Returns(false);
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(peer, new NetworkMissionLeft("stale", InstanceId));
        DrainGameThread();

        Assert.False(departed.HasValue);
    }

    [Fact]
    public void MissionLeft_RemovesEntryThatWasStillQueued()
    {
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51012), 12);
        var messageBroker = new TestMessageBroker();
        var missionManager = new MissionManager();
        var playerManager = CreatePlayerManager(peer, "current");
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager, new TestNetwork(), playerManager.Object);

        messageBroker.Publish(peer, new NetworkMissionEntered("current", InstanceId));
        messageBroker.Publish(peer, new NetworkMissionLeft("current", InstanceId));
        DrainGameThread();

        Assert.False(missionManager.TryGetControllers(InstanceId, out _));
        Assert.False(missionManager.TryGetRelayTarget(peer, InstanceId, "current", out _));
    }

    private static TestNetwork PublishEntry(
        NetPeer newcomer,
        string newcomerControllerId,
        NetPeer existing,
        string existingControllerId,
        IAuthenticatedPeerIdentityResolver identityResolver)
    {
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        var playerManager = CreatePlayerManager(newcomer, newcomerControllerId);
        IReadOnlyList<(string controllerId, NetPeer peer)> existingMembers =
            new List<(string controllerId, NetPeer peer)>
        {
            (existingControllerId, existing),
        };
        var entry = new MissionEntryResult(
            newcomerControllerId,
            InstanceId,
            MissionEntryStatus.Entered,
            existingMembers,
            Array.Empty<MissionDeparture>(),
            isFirstMember: false);
        missionManager
            .Setup(manager => manager.TryEnterMission(
                newcomer,
                newcomerControllerId,
                InstanceId,
                out entry))
            .Returns(true);

        var network = new TestNetwork();
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, network, playerManager.Object, identityResolver);

        messageBroker.Publish(newcomer, new NetworkMissionEntered(newcomerControllerId, InstanceId));
        DrainGameThread();
        return network;
    }

    private static Mock<IPlayerManager> CreatePlayerManager(NetPeer peer, string controllerId)
    {
        var playerManager = new Mock<IPlayerManager>();
        var player = new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var mappedPlayer = player;
        var mappedPeer = peer;
        playerManager.Setup(manager => manager.TryGetPlayer(peer, out mappedPlayer)).Returns(true);
        playerManager.Setup(manager => manager.TryGetPeer(controllerId, out mappedPeer)).Returns(true);
        return playerManager;
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    private static NetPeer CreatePeer(IPEndPoint endpoint, int id)
        => (NetPeer)PeerConstructor.Invoke(new object[] { new NetManager(null), endpoint, id });

    private static void MapIdentity(
        Mock<IAuthenticatedPeerIdentityResolver> identityResolver,
        IPEndPoint endpoint,
        PlatformIdentity identity)
    {
        var mappedIdentity = identity;
        identityResolver
            .Setup(resolver => resolver.TryGetIdentity(
                It.Is<IPEndPoint>(actual => actual.Equals(endpoint)),
                out mappedIdentity))
            .Returns(true);
    }
}
