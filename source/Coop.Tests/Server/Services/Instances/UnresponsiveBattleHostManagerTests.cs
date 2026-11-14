using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Instances;
using Coop.Tests.Extensions;
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

    // Against the real 15 s limit: 20 s unheard is silent, 1 s is heard.
    [Theory]
    [InlineData(20000f, true, true)]
    [InlineData(1000f, true, false)]
    [InlineData(20000f, false, false)]
    public void DisconnectsOnlyASilentHostWithASuccessorWaiting(float silenceMs, bool successorWaiting, bool hostDropped)
    {
        var host = CreatePeer(1);
        var member = CreatePeer(2);
        host.SetTimeSinceLastPacket(silenceMs);
        member.SetTimeSinceLastPacket(silenceMs);
        var missions = new MissionManager(Mock.Of<IPlayerManager>());
        Assert.True(missions.TryEnterMission(host, "host", Battle, out _));
        Assert.True(missions.TryEnterMission(member, "member", Battle, out _));
        var hosts = new BattleHostRegistry(Mock.Of<IControllerIdProvider>());
        hosts.Set(Battle, new BattleHostAssignment("host", successorWaiting ? new[] { "member" } : Array.Empty<string>(), 1, isNaval: true));
        var terminator = new Mock<IPeerTerminator>();

        new UnresponsiveBattleHostManager(new NetworkConfig(), missions, hosts, terminator.Object).DisconnectUnresponsiveHosts();

        terminator.Verify(value => value.Disconnect(host, UnresponsiveBattleHostManager.DisconnectReason),
            hostDropped ? Times.Once() : Times.Never());
        terminator.Verify(value => value.Disconnect(member, It.IsAny<string>()), Times.Never());
    }

    // A connected peer on its own endpoint, as the server holds one per player.
    private static NetPeer CreatePeer(int id)
        => (NetPeer)PeerConstructor.Invoke(new object[] { new NetManager(null), new IPEndPoint(IPAddress.Loopback, 52100 + id), id });
}
