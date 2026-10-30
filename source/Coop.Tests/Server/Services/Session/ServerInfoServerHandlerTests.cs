using Common;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Session;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using GameInterface.Configuration;
using GameInterface.Services.UI.ServerInfo;
using Moq;
using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

/// <summary>Tests the server info sent to a player after their campaign sync.</summary>
[Collection(ServerInfoLogCollection.Name)]
public sealed class ServerInfoServerHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly ModConfigData config = new();
    private readonly Mock<IModConfig> modConfig = new();

    public ServerInfoServerHandlerTests()
    {
        modConfig.SetupGet(value => value.Data).Returns(config);
    }

    [Fact]
    public void CampaignSync_SendsTheWholeInfoAsOneMessageToThatPeerOnly()
    {
        config.ServerInfo.Motd = new[] { "Welcome to EU-1", "Restart 06:00 UTC" };
        config.ServerInfo.Rules = new[] { "No griefing" };
        config.ServerInfo.Links = new[] { new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" } };
        config.ServerInfo.News = new[] { new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." } };
        var joiningPeer = network.CreatePeer();
        var otherPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(joiningPeer, new PlayerCampaignSynchronized(joiningPeer));
        DrainGameThread();

        var info = Assert.IsType<NetworkServerInfo>(Assert.Single(network.GetPeerMessages(joiningPeer)));
        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, info.Motd);
        Assert.Equal(new[] { "No griefing" }, info.Rules);
        var link = Assert.Single(info.Links);
        Assert.Equal(("Discord", "https://discord.gg/example"), (link.Label, link.Url));
        var news = Assert.Single(info.News);
        Assert.Equal(("28 Sep 2026", "Siege weekend", "Double renown."), (news.Date, news.Title, news.Text));
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
        if (key == "motd") config.ServerInfo.Motd = new[] { "Welcome to EU-1" };
        if (key == "rules") config.ServerInfo.Rules = new[] { "No griefing" };
        if (key == "links") config.ServerInfo.Links = new[] { new ServerInfoLink { Label = "", Url = "https://example.com/" } };
        if (key == "news") config.ServerInfo.News = new[] { new ServerInfoNews { Date = "", Title = "Siege weekend", Text = "" } };
        var peer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        var info = Assert.Single(network.GetPeerMessagesFromType<NetworkServerInfo>(peer));
        Assert.Equal(1, info.Motd.Length + info.Rules.Length + info.Links.Length + info.News.Length);
    }

    // No block, an empty block or a block set to null: every server today.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CampaignSync_WithoutServerInfo_SendsNothing(bool nullBlock)
    {
        if (nullBlock) config.ServerInfo = null;
        var peer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        Assert.Empty(network.SentNetworkMessages);
        Assert.Empty(network.ImmediateSends);
    }

    // The config is read and checked once per session, however many players join.
    [Fact]
    public void EveryCampaignSync_SendsTheInfoAgain_FromOneRead()
    {
        config.ServerInfo.Motd = new[] { "Welcome to EU-1" };
        var peer = network.CreatePeer();
        var rejoinedPeer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        broker.Publish(rejoinedPeer, new PlayerCampaignSynchronized(rejoinedPeer));
        DrainGameThread();

        Assert.Equal(2, network.GetPeerMessagesFromType<NetworkServerInfo>(peer).Count());
        Assert.Single(network.GetPeerMessagesFromType<NetworkServerInfo>(rejoinedPeer));
        modConfig.VerifyGet(value => value.Data, Times.Once);
    }

    // The link rules run on the server, so a bad address never reaches the wire and a good one arrives normalized.
    [Fact]
    public void CampaignSync_SendsOnlyCheckedLinks()
    {
        config.ServerInfo.Links = new[]
        {
            new ServerInfoLink { Label = "Bad", Url = "javascript:void(0)" },
            new ServerInfoLink { Label = "Shop", Url = "https://bücher.example/" },
        };
        var peer = network.CreatePeer();
        using var handler = CreateHandler();

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        var info = Assert.Single(network.GetPeerMessagesFromType<NetworkServerInfo>(peer));
        var link = Assert.Single(info.Links);
        Assert.Equal(("Shop", "https://xn--bcher-kva.example/"), (link.Label, link.Url));
        Assert.Empty(info.Motd);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        config.ServerInfo.Motd = new[] { "Welcome to EU-1" };
        var peer = network.CreatePeer();
        var handler = CreateHandler();
        Assert.Equal(1, broker.GetTotalSubscribers());

        handler.Dispose();
        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        DrainGameThread();

        Assert.Equal(0, broker.GetTotalSubscribers());
        Assert.Empty(network.SentNetworkMessages);
    }

    // A join published before Dispose has its send queued on the game thread; the disposed flag keeps it off the wire.
    [Fact]
    public void SendQueuedBeforeDispose_IsNotSent()
    {
        config.ServerInfo.Motd = new[] { "Welcome to EU-1" };
        var peer = network.CreatePeer();
        var handler = CreateHandler();
        using var gate = new ManualResetEventSlim(false);
        // Holds the game loop, so the send queued below cannot run before Dispose.
        GameThread.Run(() => { gate.Wait(TimeSpan.FromSeconds(10)); }, label: nameof(SendQueuedBeforeDispose_IsNotSent));

        broker.Publish(peer, new PlayerCampaignSynchronized(peer));
        handler.Dispose();
        gate.Set();
        DrainGameThread();

        Assert.Empty(network.SentNetworkMessages);
    }

    public void Dispose()
    {
        network.Dispose();
        broker.Dispose();
    }

    private ServerInfoServerHandler CreateHandler()
    {
        return new ServerInfoServerHandler(broker, network, modConfig.Object, new ServerInfoMessageBuilder(new ServerInfoLinkRules()));
    }

    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true, label: nameof(ServerInfoServerHandlerTests));
    }
}
