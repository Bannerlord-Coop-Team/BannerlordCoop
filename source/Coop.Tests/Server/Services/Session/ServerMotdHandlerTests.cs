using Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using GameInterface.Services.Chat.Messages;
using Moq;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

/// <summary>Tests the MOTD sent to a player after their campaign sync.</summary>
public sealed class ServerMotdHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<IServerInfoConfig> serverInfo = new();

    [Fact]
    public void CampaignSync_SendsEachLineAsSystemChatToThatPeerOnly()
    {
        serverInfo.SetupGet(config => config.Motd).Returns(new[] { "Welcome to EU-1", "Restart 06:00 UTC" });
        var joiningPeer = network.CreatePeer();
        var otherPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(joiningPeer, new PlayerCampaignSynchronized(joiningPeer));
        DrainGameThread();

        var lines = network.GetPeerMessagesFromType<NetworkChatMessage>(joiningPeer).ToArray();
        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, lines.Select(line => line.Text));
        Assert.All(lines, line =>
        {
            Assert.Equal(ChatChannel.System, line.Channel);
            Assert.Equal(string.Empty, line.SenderControllerId);
            Assert.Equal("System", line.SenderName);
            Assert.Equal(string.Empty, line.RecipientControllerId);
        });
        Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
        Assert.Equal(2, network.ImmediateSends.Count(send => ReferenceEquals(send.Peer, joiningPeer)));
    }

    [Fact]
    public void CampaignSync_WithoutMotd_SendsNothing()
    {
        serverInfo.SetupGet(config => config.Motd).Returns(Array.Empty<string>());
        var peer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        Assert.Empty(network.SentNetworkMessages);
        Assert.Empty(network.ImmediateSends);
    }

    [Fact]
    public void EveryCampaignSync_SendsTheMotdAgain()
    {
        serverInfo.SetupGet(config => config.Motd).Returns(new[] { "Welcome to EU-1" });
        var peer = network.CreatePeer();
        var rejoinedPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        broker.Publish(rejoinedPeer, new PlayerCampaignSynchronized(rejoinedPeer));
        DrainGameThread();

        Assert.Equal(2, network.GetPeerMessagesFromType<NetworkChatMessage>(peer).Count());
        Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(rejoinedPeer));
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        serverInfo.SetupGet(config => config.Motd).Returns(new[] { "Welcome to EU-1" });
        var peer = network.CreatePeer();
        var handler = CreateHandler();
        Assert.Equal(1, broker.GetTotalSubscribers());

        handler.Dispose();
        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        Assert.Equal(0, broker.GetTotalSubscribers());
        Assert.Empty(network.SentNetworkMessages);
    }

    public void Dispose()
    {
        network.Dispose();
        broker.Dispose();
    }

    private ServerMotdHandler CreateHandler()
    {
        return new ServerMotdHandler(broker, network, serverInfo.Object);
    }

    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true, label: nameof(ServerMotdHandlerTests));
    }
}
