using Common.Network;
using Common.Tests.Utils;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Handlers;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

/// <summary>Verifies <see cref="SettlementHandler"/> releases its broker subscriptions.</summary>
public class SettlementHandlerTests
{
    [Fact]
    public void Dispose_RemovesAllHandlers()
    {
        var broker = new TestMessageBroker();
        var handler = new SettlementHandler(broker, new Mock<INetwork>().Object, new Mock<IObjectManager>().Object);

        Assert.True(broker.GetTotalSubscribers() > 0);

        handler.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }

    [Fact]
    public void Dispose_KeepsOtherInstanceSubscribed()
    {
        var broker = new TestMessageBroker();
        var network = new Mock<INetwork>().Object;
        var objectManager = new Mock<IObjectManager>().Object;
        var first = new SettlementHandler(broker, network, objectManager);
        int handlerSubscribers = broker.GetTotalSubscribers();
        var second = new SettlementHandler(broker, network, objectManager);

        first.Dispose();

        Assert.Equal(handlerSubscribers, broker.GetTotalSubscribers());

        second.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }
}
