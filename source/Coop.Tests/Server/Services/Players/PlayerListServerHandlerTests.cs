using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using Common.Network;
using Coop.Core.Server;
using Coop.Core.Server.Services.Players.Handlers;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.PlayerList;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server.Services.Players;

/// <summary>Tests roster projection without creating a second player lifecycle.</summary>
public class PlayerListServerHandlerTests
{
    // A late join receives the cached full roster even without another campaign-state change.
    [Fact]
    public void LateJoinReceivesUnchangedOfflineRoster()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
        var player = new Player("offline", "hero", "party", "clan", "character");
        var players = new Mock<IPlayerManager>();
        players.SetupGet(p => p.Players).Returns(new[] { player });
        var activity = new Mock<IPlayerActivityReader>();
        activity.Setup(a => a.Read(player, false)).Returns(new PlayerListEntry { ControllerId = "offline" });
        using var broker = new MessageBroker();
        var network = new Mock<INetwork>();
        var peer = new TestNetwork().CreatePeer();
        using var received = new ManualResetEventSlim();
        NetworkPlayerList? baseline = null;
        network.Setup(n => n.Send(peer, It.IsAny<NetworkPlayerList>())).Callback<LiteNetLib.NetPeer, IMessage>((_, message) =>
        {
            baseline = (NetworkPlayerList)message;
            received.Set();
        });
        using var handler = new PlayerListServerHandler(broker, network.Object, players.Object, activity.Object);
        handler.PublishChanges();
        broker.Publish(this, new PlayerCampaignSynchronized(peer));
        Assert.True(received.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal("offline", Assert.Single(baseline!.Entries).ControllerId);
        activity.Verify(a => a.Read(player, false), Times.Once);
    }

    // Retains disconnected registrations, excludes the dedicated server and suppresses identical snapshots.
    [Fact]
    public void ExistingRegistryDrivesDisconnectReconnectAndChangeDetection()
    {
        var player = new Player("client", "hero", "party", "clan", "character");
        var host = new Player(CoopServer.ServerControllerId, "", "", "", "");
        var players = new Mock<IPlayerManager>();
        players.SetupGet(p => p.Players).Returns(new[] { player, host });
        var online = true;
        players.Setup(p => p.IsConnected(player)).Returns(() => online);
        var activity = new Mock<IPlayerActivityReader>();
        activity.Setup(a => a.Read(player, It.IsAny<bool>())).Returns((Player p, bool connected) =>
            new PlayerListEntry { ControllerId = p.ControllerId, Online = connected, Activity = connected ? PlayerActivity.Idle : PlayerActivity.None });
        var network = new Mock<INetwork>();
        using var handler = new PlayerListServerHandler(Mock.Of<IMessageBroker>(), network.Object, players.Object, activity.Object);
        handler.PublishChanges();
        handler.PublishChanges();
        network.Verify(n => n.SendAll(It.IsAny<NetworkPlayerList>()), Times.Once);
        online = false;
        handler.PublishChanges();
        Assert.False(Assert.Single(handler.Capture()).Online);
        online = true;
        handler.PublishChanges();
        Assert.True(Assert.Single(handler.Capture()).Online);
        network.Verify(n => n.SendAll(It.IsAny<NetworkPlayerList>()), Times.Exactly(3));
        activity.Verify(a => a.Read(host, It.IsAny<bool>()), Times.Never);
    }
}
