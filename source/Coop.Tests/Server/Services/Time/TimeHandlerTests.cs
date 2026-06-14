using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using Moq;
using Xunit;

namespace Coop.Tests.Server.Services.Time;

public class TimeHandlerTests
{
    [Fact]
    public void Dispose_RemovesAllHandlers()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, timeControlInterface.Object);

        Assert.True(broker.GetTotalSubscribers() > 0);

        // Act
        handler.Dispose();

        // Assert
        Assert.Equal(0, broker.GetTotalSubscribers());
    }

    [Fact]
    public void LoadingPlayersChanged_WhenPlayersLoading_PausesAndLocksClients()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, timeControlInterface.Object);
        var peer = network.CreatePeer();
        var payload = new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(2));

        // Act
        handler.Handle_LoadingPlayersChanged(payload);

        // Assert
        timeControlInterface.Verify(m => m.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once);
        var lockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(peer));
        Assert.True(lockMessage.IsLocked);
        Assert.Equal(2, lockMessage.LoadingPlayers);
    }

    [Fact]
    public void LoadingPlayersChanged_WhenNoPlayersLoading_UnlocksClients()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, timeControlInterface.Object);
        var peer = network.CreatePeer();
        var payload = new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(0));

        // Act
        handler.Handle_LoadingPlayersChanged(payload);

        // Assert
        timeControlInterface.Verify(m => m.ServerSetTimeControl(It.IsAny<TimeControlEnum>()), Times.Never);
        var lockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(peer));
        Assert.False(lockMessage.IsLocked);
        Assert.Equal(0, lockMessage.LoadingPlayers);
    }
}
