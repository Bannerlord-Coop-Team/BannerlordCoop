using Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

/// <summary>Tests the server info sent to a player after their campaign sync.</summary>
public sealed class ServerInfoServerHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly StubServerInfo serverInfo = new();
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "BannerlordCoop-ServerInfoServerHandlerTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CampaignSync_SendsTheWholeInfoAsOneMessageToThatPeerOnly()
    {
        serverInfo.Motd = new[] { "Welcome to EU-1", "Restart 06:00 UTC" };
        serverInfo.Rules = new[] { "No griefing" };
        serverInfo.Links = new[] { new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" } };
        serverInfo.News = new[] { new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." } };
        var joiningPeer = network.CreatePeer();
        var otherPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(joiningPeer, new PlayerCampaignSynchronized(joiningPeer));
        DrainGameThread();

        var info = Assert.IsType<NetworkServerInfo>(Assert.Single(network.GetPeerMessages(joiningPeer)));
        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, info.Motd);
        Assert.Equal(new[] { "No griefing" }, info.Rules);
        Assert.Equal(serverInfo.Links, info.Links);
        Assert.Equal(serverInfo.News, info.News);
        Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
        Assert.Empty(network.ImmediateSends);
    }

    [Theory]
    [InlineData("motd")]
    [InlineData("rules")]
    [InlineData("links")]
    [InlineData("news")]
    public void CampaignSync_WithOneKey_SendsIt(string key)
    {
        if (key == "motd") serverInfo.Motd = new[] { "Welcome to EU-1" };
        if (key == "rules") serverInfo.Rules = new[] { "No griefing" };
        if (key == "links") serverInfo.Links = new[] { new ServerInfoLink { Label = "", Url = "https://example.com/" } };
        if (key == "news") serverInfo.News = new[] { new ServerInfoNews { Date = "", Title = "Siege weekend", Text = "" } };
        var peer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        var info = Assert.Single(network.GetPeerMessagesFromType<NetworkServerInfo>(peer));
        Assert.Equal(1, info.Motd.Length + info.Rules.Length + info.Links.Length + info.News.Length);
    }

    [Fact]
    public void CampaignSync_WithoutServerInfo_SendsNothing()
    {
        var peer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        Assert.Empty(network.SentNetworkMessages);
        Assert.Empty(network.ImmediateSends);
    }

    [Fact]
    public void EveryCampaignSync_SendsTheInfoAgain()
    {
        serverInfo.Motd = new[] { "Welcome to EU-1" };
        var peer = network.CreatePeer();
        var rejoinedPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        broker.Publish(rejoinedPeer, new PlayerCampaignSynchronized(rejoinedPeer));
        DrainGameThread();

        Assert.Equal(2, network.GetPeerMessagesFromType<NetworkServerInfo>(peer).Count());
        Assert.Single(network.GetPeerMessagesFromType<NetworkServerInfo>(rejoinedPeer));
    }

    // Uses the real file reader, so the caps, a broken escape and a bad link reach the wire the way an operator writes them.
    [Fact]
    public void CampaignSync_SendsTheCappedInfoFromTheFile()
    {
        var paragraphs = Enumerable.Range(1, 12).Select(index => "\"Paragraph " + index + "\"").ToList();
        paragraphs.Insert(1, "\"\\ud83d\"");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "server-info.json");
        File.WriteAllText(path, "{\"motd\":[" + string.Join(",", paragraphs) + "]," +
            "\"links\":[{\"label\":\"Bad\",\"url\":\"javascript:void(0)\"},{\"label\":\"Shop\",\"url\":\"https://bücher.example/\"}]}");
        var peer = network.CreatePeer();
        using var handler = new ServerInfoServerHandler(broker, network, new ServerInfoConfig(path, new ServerInfoLinkRules()));

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        var info = Assert.Single(network.GetPeerMessagesFromType<NetworkServerInfo>(peer));
        Assert.Equal(Enumerable.Range(1, ServerInfoLimits.MaxMotdParagraphs).Select(index => "Paragraph " + index), info.Motd);
        var link = Assert.Single(info.Links);
        Assert.Equal(("Shop", "https://xn--bcher-kva.example/"), (link.Label, link.Url));
        Assert.Empty(info.Rules);
        Assert.Empty(info.News);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        serverInfo.Motd = new[] { "Welcome to EU-1" };
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

    private ServerInfoServerHandler CreateHandler()
    {
        return new ServerInfoServerHandler(broker, network, serverInfo);
    }

    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true, label: nameof(ServerInfoServerHandlerTests));
    }

    private sealed class StubServerInfo : IServerInfoConfig
    {
        public IReadOnlyList<string> Motd { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Rules { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ServerInfoLink> Links { get; set; } = Array.Empty<ServerInfoLink>();
        public IReadOnlyList<ServerInfoNews> News { get; set; } = Array.Empty<ServerInfoNews>();
    }
}
