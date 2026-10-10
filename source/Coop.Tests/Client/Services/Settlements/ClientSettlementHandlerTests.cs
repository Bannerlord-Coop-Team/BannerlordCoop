using Common.Tests.Utils;
using Coop.Core.Client.Services.Settlements.Handlers;
using Coop.Core.Server.Services.Settlements.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Settlements.Audit;
using System;
using Xunit;

namespace Coop.Tests.Client.Services.Settlements;

/// <summary>Verifies <see cref="ClientSettlementHandler"/> releases its broker subscriptions.</summary>
public class ClientSettlementHandlerTests
{
    [Fact]
    public void Dispose_RemovesAllHandlers()
    {
        var broker = new TestMessageBroker();
        var handler = new ClientSettlementHandler(broker, new TestNetwork());

        Assert.True(broker.GetTotalSubscribers() > 0);

        handler.Dispose();
        handler.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }

    [Fact]
    public void Dispose_KeepsOtherInstanceSubscribed()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var first = new ClientSettlementHandler(broker, network);
        int handlerSubscribers = broker.GetTotalSubscribers();
        var second = new ClientSettlementHandler(broker, network);

        first.Dispose();

        Assert.Equal(handlerSubscribers, broker.GetTotalSubscribers());

        second.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }

    [Fact]
    public void AuditResults_AfterDispose_OnlyLiveHandlerResponds()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var disposed = new ClientSettlementHandler(broker, network);
        var live = new ClientSettlementHandler(broker, network);
        disposed.Dispose();

        broker.Publish(this, new NetworkSettlementAuditResults(Array.Empty<SettlementAuditData>(), string.Empty));

        Assert.Single(broker.GetMessagesFromType<SettlementAuditResponse>());
        GC.KeepAlive(disposed);
        GC.KeepAlive(live);
    }
}
