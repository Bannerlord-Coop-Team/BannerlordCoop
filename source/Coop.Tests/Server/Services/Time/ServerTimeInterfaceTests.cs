using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Interaces;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Server.Services.Time;

public class ServerTimeInterfaceTests
{
    [Fact]
    public void PlayersLoadingPolicy_WhenPlayersLoading_ReturnsFalse()
    {
        // Arrange
        Func<bool>? policy = null;
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var clientRegistry = new Mock<IConnectionCollection>();
        var timeControlInterface = new Mock<ITimeControlInterface>();

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(true);
        clientRegistry.SetupGet(m => m.LoadingPeers).Returns(new List<NetPeer> { peer });
        timeControlInterface
            .Setup(m => m.AddUnpausePolicy(It.IsAny<Func<bool>>()))
            .Callback<Func<bool>>(p => policy = p);

        _ = new ServerTimeInterface(timeControlInterface.Object, clientRegistry.Object);

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
        var clientRegistry = new Mock<IConnectionCollection>();
        var timeControlInterface = new Mock<ITimeControlInterface>();

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(false);
        timeControlInterface
            .Setup(m => m.AddUnpausePolicy(It.IsAny<Func<bool>>()))
            .Callback<Func<bool>>(p => policy = p);

        _ = new ServerTimeInterface(timeControlInterface.Object, clientRegistry.Object);

        // Act
        Assert.NotNull(policy);
        var result = policy();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Dispose_RemovesUnpausePolicy()
    {
        // Arrange
        var clientRegistry = new Mock<IConnectionCollection>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var serverTimeInterface = new ServerTimeInterface(timeControlInterface.Object, clientRegistry.Object);

        // Act
        serverTimeInterface.Dispose();

        // Assert
        timeControlInterface.Verify(m => m.RemoveUnpausePolicy(It.IsAny<Func<bool>>()), Times.Once);
    }
}
