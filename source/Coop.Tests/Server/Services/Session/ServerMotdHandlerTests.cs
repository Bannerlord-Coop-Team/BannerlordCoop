using Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using GameInterface.Services.UI.Motd;
using Moq;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

/// <summary>Tests the MOTD sent to a player after their campaign sync.</summary>
public sealed class ServerMotdHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<IServerInfoConfig> serverInfo = new();
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "BannerlordCoop-ServerMotdHandlerTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CampaignSync_SendsTheWholeMotdAsOneMessageToThatPeerOnly()
    {
        serverInfo.SetupGet(config => config.Motd).Returns(new[] { "Welcome to EU-1", "Restart 06:00 UTC" });
        var joiningPeer = network.CreatePeer();
        var otherPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(joiningPeer, new PlayerCampaignSynchronized(joiningPeer));
        DrainGameThread();

        var message = Assert.Single(network.GetPeerMessages(joiningPeer));
        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, Assert.IsType<NetworkMotd>(message).Paragraphs);
        Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
        Assert.Empty(network.ImmediateSends);
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

        Assert.Equal(2, network.GetPeerMessagesFromType<NetworkMotd>(peer).Count());
        Assert.Single(network.GetPeerMessagesFromType<NetworkMotd>(rejoinedPeer));
    }

    // Uses the real file reader, so the caps and a broken escape reach the wire the way an operator writes them.
    [Fact]
    public void CampaignSync_SendsTheCappedMotdFromTheFile()
    {
        var paragraphs = Enumerable.Range(1, 12).Select(index => "\"Rule " + index + "\"").ToList();
        paragraphs.Insert(1, "\"\\ud83d\"");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "server-info.json");
        File.WriteAllText(path, "{\"motd\":[" + string.Join(",", paragraphs) + "]}");
        var peer = network.CreatePeer();
        using var handler = new ServerMotdHandler(broker, network, new ServerInfoConfig(path));

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        var motd = Assert.Single(network.GetPeerMessagesFromType<NetworkMotd>(peer));
        Assert.Equal(Enumerable.Range(1, MotdLimits.MaxParagraphs).Select(index => "Rule " + index), motd.Paragraphs);
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
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
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
