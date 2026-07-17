using Autofac;
using Common.Network.Messages;
using Common.Network.Session;
using Common.Tests.Utils;
using Coop.Core.Common.Session;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.Missions;
using LiteNetLib;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Instances;

public class ServerMissionMembershipHandlerTests
{
    private const string InstanceId = "battle-1";
    private readonly ITestOutputHelper output;

    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    public ServerMissionMembershipHandlerTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ServerTestComponent_ResolvesMissionMembershipRegistry()
    {
        using var container = new ServerTestComponent(output).Container;

        Assert.IsType<MissionMembershipRegistry>(container.Resolve<IMissionMembershipRegistry>());
    }

    [Fact]
    public void MissionEntered_FansOutTheOppositePeersTunnelSteamId()
    {
        RunWithManagedServer(ownerProcessId: 42, () =>
        {
            var newcomerEndpoint = new IPEndPoint(IPAddress.Loopback, 51001);
            var existingEndpoint = new IPEndPoint(IPAddress.Loopback, 51002);
            var newcomer = CreatePeer(newcomerEndpoint, 1);
            var existing = CreatePeer(existingEndpoint, 2);

            var tunnelHost = new Mock<ISessionTunnelIdentityResolver>();
            MapSteamId(tunnelHost, newcomerEndpoint, 9001);
            MapSteamId(tunnelHost, existingEndpoint, 9002);

            var network = PublishEntry(
                newcomer, "1111", existing, "2222", tunnelHost.Object);

            var sentToExisting = Assert.Single(
                network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(existing));
            Assert.Equal("1111", sentToExisting.ControllerId);
            Assert.Equal(9001UL, sentToExisting.SteamId);

            var sentToNewcomer = Assert.Single(
                network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(newcomer));
            Assert.Equal("2222", sentToNewcomer.ControllerId);
            Assert.Equal(9002UL, sentToNewcomer.SteamId);
        });
    }

    [Fact]
    public void MissionEntered_ManagedDirectLoopbackHostFallsBackToNumericControllerId()
    {
        RunWithManagedServer(ownerProcessId: 42, () =>
        {
            const string hostControllerId = "76561198000000042";
            var hostEndpoint = new IPEndPoint(IPAddress.Loopback, 51003);
            var tunneledEndpoint = new IPEndPoint(IPAddress.Loopback, 51004);
            var host = CreatePeer(hostEndpoint, 3);
            var tunneledPeer = CreatePeer(tunneledEndpoint, 4);

            var tunnelHost = new Mock<ISessionTunnelIdentityResolver>();
            MapSteamId(tunnelHost, tunneledEndpoint, 9004);

            var network = PublishEntry(
                host, hostControllerId, tunneledPeer, "remote", tunnelHost.Object);

            var sentToRemote = Assert.Single(
                network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(tunneledPeer));
            Assert.Equal(76561198000000042UL, sentToRemote.SteamId);

            var sentToHost = Assert.Single(
                network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(host));
            Assert.Equal(9004UL, sentToHost.SteamId);
        });
    }

    [Theory]
    [InlineData(0, "127.0.0.1")]
    [InlineData(42, "203.0.113.10")]
    public void MissionEntered_UnmappedNonHostPeerKeepsRelayFallback(int ownerProcessId, string address)
    {
        RunWithManagedServer(ownerProcessId, () =>
        {
            var newcomer = CreatePeer(new IPEndPoint(IPAddress.Parse(address), 51005), 5);
            var existing = CreatePeer(new IPEndPoint(IPAddress.Parse("203.0.113.11"), 51006), 6);
            var tunnelHost = new Mock<ISessionTunnelIdentityResolver>();

            var network = PublishEntry(
                newcomer, "76561198000000042", existing, "existing", tunnelHost.Object);

            var sentToExisting = Assert.Single(
                network.GetPeerMessagesFromType<NetworkMissionPeerEntered>(existing));
            Assert.Equal(0UL, sentToExisting.SteamId);
        });
    }

    [Fact]
    public void MissionEnteredAndLeft_UpdatesMembershipRegistry()
    {
        var messageBroker = new TestMessageBroker();
        var membershipRegistry = new MissionMembershipRegistry();
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51007), 7);
        var tunnelHost = new Mock<ISessionTunnelIdentityResolver>();

        using var handler = new ServerMissionMembershipHandler(
            messageBroker,
            new MissionManager(),
            new TestNetwork(),
            tunnelHost.Object,
            membershipRegistry);

        messageBroker.Publish(peer, new NetworkMissionEntered("controller", InstanceId));
        Assert.True(membershipRegistry.IsControllerInMission("controller"));

        messageBroker.Publish(peer, new NetworkMissionLeft("controller", InstanceId));
        Assert.False(membershipRegistry.IsControllerInMission("controller"));
    }

    [Fact]
    public void ReplacedPeer_StaleLeaveKeepsCurrentMissionMembership()
    {
        const string controllerId = "controller";
        var messageBroker = new TestMessageBroker();
        var membershipRegistry = new MissionMembershipRegistry();
        var missionManager = new MissionManager();
        var network = new TestNetwork();
        var oldPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51008), 8);
        var newPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51009), 9);

        using var handler = new ServerMissionMembershipHandler(
            messageBroker,
            missionManager,
            network,
            new Mock<ISessionTunnelIdentityResolver>().Object,
            membershipRegistry);

        messageBroker.Publish(oldPeer, new NetworkMissionEntered(controllerId, InstanceId));
        messageBroker.Publish(newPeer, new NetworkMissionEntered(controllerId, InstanceId));
        messageBroker.Messages.Clear();
        network.Clear();

        messageBroker.Publish(oldPeer, new NetworkMissionLeft(controllerId, InstanceId));

        Assert.True(membershipRegistry.IsControllerInMission(controllerId));
        Assert.True(missionManager.TryGetRelayTarget(InstanceId, controllerId, out var currentPeer));
        Assert.Same(newPeer, currentPeer);
        Assert.Empty(network.SentNetworkMessages);
        Assert.Empty(messageBroker.GetMessagesFromType<MissionMemberDeparted>());

        messageBroker.Publish(newPeer, new NetworkMissionLeft(controllerId, InstanceId));

        Assert.False(membershipRegistry.IsControllerInMission(controllerId));
        Assert.False(missionManager.TryGetRelayTarget(InstanceId, controllerId, out _));
        Assert.Single(messageBroker.GetMessagesFromType<MissionMemberDeparted>());
    }

    [Fact]
    public void ReplacedPeer_StaleDisconnectKeepsCurrentMissionMembership()
    {
        const string controllerId = "controller";
        var messageBroker = new TestMessageBroker();
        var membershipRegistry = new MissionMembershipRegistry();
        var missionManager = new MissionManager();
        var oldPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51010), 10);
        var newPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51011), 11);

        using var handler = new ServerMissionMembershipHandler(
            messageBroker,
            missionManager,
            new TestNetwork(),
            new Mock<ISessionTunnelIdentityResolver>().Object,
            membershipRegistry);

        messageBroker.Publish(oldPeer, new NetworkMissionEntered(controllerId, InstanceId));
        messageBroker.Publish(newPeer, new NetworkMissionEntered(controllerId, InstanceId));
        messageBroker.Messages.Clear();

        messageBroker.Publish(this, new PlayerDisconnected(oldPeer, default));

        Assert.True(membershipRegistry.IsControllerInMission(controllerId));
        Assert.True(missionManager.TryGetRelayTarget(InstanceId, controllerId, out var currentPeer));
        Assert.Same(newPeer, currentPeer);
        Assert.Empty(messageBroker.GetMessagesFromType<MissionMemberDeparted>());

        messageBroker.Publish(this, new PlayerDisconnected(newPeer, default));

        Assert.False(membershipRegistry.IsControllerInMission(controllerId));
        Assert.False(missionManager.TryGetRelayTarget(InstanceId, controllerId, out _));
        Assert.Single(messageBroker.GetMessagesFromType<MissionMemberDeparted>());
    }

    [Fact]
    public void MissionManager_WrongControllerLeaveDoesNotRemoveCurrentPeer()
    {
        var missionManager = new MissionManager();
        var peer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 51012), 12);
        missionManager.EnterMission(peer, "controller", InstanceId);

        Assert.False(missionManager.TryLeaveMission(
            peer,
            "different-controller",
            InstanceId,
            out var remaining));
        Assert.Empty(remaining);
        Assert.True(missionManager.TryGetRelayTarget(InstanceId, "controller", out var currentPeer));
        Assert.Same(peer, currentPeer);
    }

    private static TestNetwork PublishEntry(
        NetPeer newcomer,
        string newcomerControllerId,
        NetPeer existing,
        string existingControllerId,
        ISessionTunnelIdentityResolver tunnelHost)
    {
        var messageBroker = new TestMessageBroker();
        var missionManager = new Mock<IMissionManager>();
        missionManager
            .Setup(manager => manager.EnterMission(newcomer, newcomerControllerId, InstanceId))
            .Returns(new List<(string controllerId, NetPeer peer)>
            {
                (existingControllerId, existing),
            });

        var network = new TestNetwork();
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager.Object, network, tunnelHost, new MissionMembershipRegistry());

        messageBroker.Publish(newcomer, new NetworkMissionEntered(newcomerControllerId, InstanceId));
        return network;
    }

    private static NetPeer CreatePeer(IPEndPoint endpoint, int id)
        => (NetPeer)PeerConstructor.Invoke(new object[] { new NetManager(null), endpoint, id });

    private static void MapSteamId(
        Mock<ISessionTunnelIdentityResolver> tunnelHost,
        IPEndPoint endpoint,
        ulong steamId)
    {
        var mappedSteamId = steamId;
        tunnelHost
            .Setup(host => host.TryGetRemoteSteamId(
                It.Is<IPEndPoint>(actual => actual.Equals(endpoint)),
                out mappedSteamId))
            .Returns(true);
    }

    private static void RunWithManagedServer(int ownerProcessId, Action test)
    {
        var previousOwnerProcessId = ManagedServerConfig.OwnerProcessId;
        try
        {
            ManagedServerConfig.OwnerProcessId = ownerProcessId;
            test();
        }
        finally
        {
            ManagedServerConfig.OwnerProcessId = previousOwnerProcessId;
        }
    }
}
