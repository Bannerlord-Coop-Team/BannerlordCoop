using Common.Tests.Utils;
using Coop.Core.Server.Services.Settlements.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using Moq;
using Xunit;

namespace Coop.Tests.Server.Services.Settlements;

/// <summary>Verifies <see cref="ServerSettlementHandler"/> releases its broker subscriptions.</summary>
public class ServerSettlementHandlerTests
{
    [Fact]
    public void Dispose_RemovesAllHandlers()
    {
        var broker = new TestMessageBroker();
        var handler = new ServerSettlementHandler(broker, new TestNetwork(), new Mock<IObjectManager>().Object);

        Assert.True(broker.GetTotalSubscribers() > 0);

        handler.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }

    [Fact]
    public void Dispose_KeepsOtherInstanceSubscribed()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var objectManager = new Mock<IObjectManager>().Object;
        var first = new ServerSettlementHandler(broker, network, objectManager);
        int handlerSubscribers = broker.GetTotalSubscribers();
        var second = new ServerSettlementHandler(broker, network, objectManager);

        first.Dispose();

        Assert.Equal(handlerSubscribers, broker.GetTotalSubscribers());

        second.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }
}
