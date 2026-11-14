using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Instances;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using LiteNetLib;
using Moq;
using System;
using System.Net;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>Only a silent battle host with a successor waiting is dropped early; every other member keeps the link timeout.</summary>
public class UnresponsiveBattleHostManagerTests
{
    private const string Battle = "MapEvent_1";

    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    // A fresh peer was last heard 0 ms ago, so a zero limit makes both members silent and an hour makes neither.
    [Theory]
    [InlineData(0, true, true)]
    [InlineData(3600, true, false)]
    [InlineData(0, false, false)]
    public void DisconnectsOnlyASilentHostWithASuccessorWaiting(int timeoutSeconds, bool successorWaiting, bool hostDropped)
    {
        var host = CreatePeer(1);
        var member = CreatePeer(2);
        var missions = new MissionManager(Mock.Of<IPlayerManager>());
        Assert.True(missions.TryEnterMission(host, "host", Battle, out _));
        Assert.True(missions.TryEnterMission(member, "member", Battle, out _));
        var hosts = new BattleHostRegistry(Mock.Of<IControllerIdProvider>());
        hosts.Set(Battle, new BattleHostAssignment("host", successorWaiting ? new[] { "member" } : Array.Empty<string>(), 1, isNaval: true));
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.BattleHostDisconnectTimeout).Returns(TimeSpan.FromSeconds(timeoutSeconds));
        var terminator = new Mock<IJoinPeerTerminator>();

        new UnresponsiveBattleHostManager(config.Object, missions, hosts, terminator.Object).DisconnectUnresponsiveHosts();

        terminator.Verify(value => value.Disconnect(host, "BattleHostUnresponsive"), hostDropped ? Times.Once() : Times.Never());
        terminator.Verify(value => value.Disconnect(member, It.IsAny<string>()), Times.Never());
    }

    // A connected peer on its own endpoint, as the server holds one per player.
    private static NetPeer CreatePeer(int id)
        => (NetPeer)PeerConstructor.Invoke(new object[] { new NetManager(null), new IPEndPoint(IPAddress.Loopback, 52100 + id), id });
}
