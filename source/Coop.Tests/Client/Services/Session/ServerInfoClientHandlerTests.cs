using Common;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Session;
using Coop.Tests.Stubs;
using GameInterface.Services.UI.ServerInfo;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Client.Services.Session;

/// <summary>Tests that the client hands the server's MOTD to the popup service on the game thread.</summary>
public sealed class ServerInfoClientHandlerTests : IDisposable
{
    private readonly StubMessageBroker broker = new();
    private readonly Mock<IServerInfoService> service = new();

    [Fact]
    public void CampaignReady_CreatesThePopup()
    {
        using var handler = new ServerInfoClientHandler(broker, service.Object);

        broker.Publish(this, new ClientCampaignReady());
        DrainGameThread();

        service.Verify(motd => motd.Initialize(), Times.Once);
    }

    [Fact]
    public void Motd_IsHandedToTheServiceAsSent()
    {
        var paragraphs = new[] { "Welcome to EU-1", "{PLAYER} <b>Restart 06:00 UTC</b>" };
        using var handler = new ServerInfoClientHandler(broker, service.Object);

        broker.Publish(this, new NetworkServerInfo(paragraphs, null, null, null));
        DrainGameThread();

        service.Verify(motd => motd.Show(paragraphs), Times.Once);
        service.Verify(motd => motd.Initialize(), Times.Never);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var handler = new ServerInfoClientHandler(broker, service.Object);
        Assert.Equal(2, broker.GetTotalSubscribers());

        handler.Dispose();
        broker.Publish(this, new ClientCampaignReady());
        broker.Publish(this, new NetworkServerInfo(new[] { "Welcome to EU-1" }, null, null, null));
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
