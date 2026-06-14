using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Server.Services.Time;

public class TimeHandlerTests
{
    [Fact]
    public void Dispose_RemovesAllHandlersAndUnpausePolicy()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var connections = new Mock<IConnectionCollection>();
        var handler = new TimeHandler(broker, network, timeControlInterface.Object, connections.Object);

        Assert.True(broker.GetTotalSubscribers() > 0);

        // Act
        handler.Dispose();

        // Assert
        Assert.Equal(0, broker.GetTotalSubscribers());
        timeControlInterface.Verify(m => m.RemoveUnpausePolicy(It.IsAny<Func<bool>>()), Times.Once);
    }

    [Fact]
    public void LoadingPlayersChanged_WhenPlayersLoading_PausesAndLocksClients()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var connections = new Mock<IConnectionCollection>();
        var handler = new TimeHandler(broker, network, timeControlInterface.Object, connections.Object);
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
        var connections = new Mock<IConnectionCollection>();
        var handler = new TimeHandler(broker, network, timeControlInterface.Object, connections.Object);
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

    [Fact]
    public void PlayersLoadingPolicy_WhenPlayersLoading_ReturnsFalse()
    {
        // Arrange
        Func<bool>? policy = null;
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var connections = new Mock<IConnectionCollection>();

        var loadingLogic = new Mock<IConnectionLogic>();
        loadingLogic.SetupGet(m => m.Peer).Returns(peer);
        connections.SetupGet(m => m.LoadingPeers).Returns(new List<IConnectionLogic> { loadingLogic.Object });
        timeControlInterface
            .Setup(m => m.AddUnpausePolicy(It.IsAny<Func<bool>>()))
            .Callback<Func<bool>>(p => policy = p);

        _ = new TimeHandler(broker, network, timeControlInterface.Object, connections.Object);

        // Act
        Assert.NotNull(policy);
        var result = policy();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PlayersLoadingPolicy_WhenNoPlayersLoading_ReturnsTrue()
    {
        // Arrange
        Func<bool>? policy = null;
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var connections = new Mock<IConnectionCollection>();

        connections.SetupGet(m => m.LoadingPeers).Returns(new List<IConnectionLogic>());
        timeControlInterface
            .Setup(m => m.AddUnpausePolicy(It.IsAny<Func<bool>>()))
            .Callback<Func<bool>>(p => policy = p);

        _ = new TimeHandler(broker, network, timeControlInterface.Object, connections.Object);

        // Act
        Assert.NotNull(policy);
        var result = policy();

        // Assert
        Assert.True(result);
    }
}
