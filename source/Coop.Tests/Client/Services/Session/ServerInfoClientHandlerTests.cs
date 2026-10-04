using Common;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Session;
using Coop.Tests.Stubs;
using GameInterface.Services.UI.ServerInfo;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Client.Services.Session;

/// <summary>Tests that the client hands the server's info to the panel service on the game thread.</summary>
public sealed class ServerInfoClientHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly Mock<IServerInfoService> service = new();

    [Fact]
    public void CampaignReady_CreatesThePanel()
    {
        using var handler = new ServerInfoClientHandler(broker, service.Object);

        broker.Publish(this, new ClientCampaignReady());
        DrainGameThread();

        service.Verify(serverInfo => serverInfo.Initialize(), Times.Once);
    }

    [Fact]
    public void ServerInfo_IsHandedToTheServiceAsSent()
    {
        var info = new NetworkServerInfo(new[] { "Welcome to EU-1", "{PLAYER} <b>Restart 06:00 UTC</b>" }, new[] { "No griefing" },
            new[] { new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" } }, null);
        using var handler = new ServerInfoClientHandler(broker, service.Object);

        broker.Publish(this, info);
        DrainGameThread();

        service.Verify(serverInfo => serverInfo.Show(info), Times.Once);
        service.Verify(serverInfo => serverInfo.Initialize(), Times.Never);
    }

    // !motd after leaving a server must not show that server's info.
    [Fact]
    public void Disconnect_ClearsTheInfo()
    {
        using var handler = new ServerInfoClientHandler(broker, service.Object);

        broker.Publish(this, new NetworkDisconnected(default));
        DrainGameThread();

        service.Verify(serverInfo => serverInfo.Clear(), Times.Once);
        service.Verify(serverInfo => serverInfo.Show(It.IsAny<NetworkServerInfo>()), Times.Never);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var handler = new ServerInfoClientHandler(broker, service.Object);
        Assert.Equal(3, broker.GetTotalSubscribers());

        handler.Dispose();
        broker.Publish(this, new ClientCampaignReady());
        broker.Publish(this, new NetworkServerInfo(new[] { "Welcome to EU-1" }, null, null, null));
        broker.Publish(this, new NetworkDisconnected(default));
        DrainGameThread();

        Assert.Equal(0, broker.GetTotalSubscribers());
        service.VerifyNoOtherCalls();
    }

    public void Dispose()
    {
        broker.Dispose();
    }

    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true, label: nameof(ServerInfoClientHandlerTests));
    }
}
