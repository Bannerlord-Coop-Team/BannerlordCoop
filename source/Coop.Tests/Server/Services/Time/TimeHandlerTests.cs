using Common.Messaging;
using Common.Network.Messages;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
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
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);

        Assert.True(broker.GetTotalSubscribers() > 0);

        // Act
        handler.Dispose();

        // Assert
        Assert.Equal(0, broker.GetTotalSubscribers());
        timeControlInterface.Verify(m => m.RemoveUnpausePolicy(It.IsAny<Func<bool>>()), Times.Once);
    }

    [Fact]
    public void PlayerConnected_ForcesPauseAndLocksClients()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);
        var peer = network.CreatePeer();
        var payload = new MessagePayload<PlayerConnected>(this, new PlayerConnected(peer));

        // Act
        handler.Handle_PlayerConnected(payload);

        // Assert
        timeControlInterface.Verify(m => m.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once);
        var lockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(peer));
        Assert.True(lockMessage.IsLocked);
        Assert.Equal(1, lockMessage.LoadingPlayers);
    }

    [Fact]
    public void PlayerCampaignEntered_WhenNoPlayersLoading_UnlocksClients()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);
        var peer = network.CreatePeer();
        var payload = new MessagePayload<PlayerCampaignEntered>(this, new PlayerCampaignEntered(peer));

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(false);

        // Act
        handler.Handle_PlayerCampaignEntered(payload);

        // Assert
        var lockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(peer));
        Assert.False(lockMessage.IsLocked);
        Assert.Equal(0, lockMessage.LoadingPlayers);
    }

    [Fact]
    public void PlayerCampaignEntered_WhenPlayersStillLoading_RefreshesClientLock()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);
        var peer = network.CreatePeer();
        var loadingPeer = network.CreatePeer();
        var payload = new MessagePayload<PlayerCampaignEntered>(this, new PlayerCampaignEntered(peer));

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(true);
        clientRegistry.SetupGet(m => m.LoadingPeers).Returns(new List<NetPeer> { loadingPeer });

        // Act
        handler.Handle_PlayerCampaignEntered(payload);

        // Assert
        var lockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(peer));
        Assert.True(lockMessage.IsLocked);
        Assert.Equal(1, lockMessage.LoadingPlayers);

        var loadingPeerLockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(loadingPeer));
        Assert.True(loadingPeerLockMessage.IsLocked);
        Assert.Equal(1, loadingPeerLockMessage.LoadingPlayers);
    }

    [Fact]
    public void PlayerDisconnected_WhenLastLoadingPeerDisconnected_UnlocksClients()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);
        var disconnectedPeer = network.CreatePeer();
        var payload = new MessagePayload<PlayerDisconnected>(
            this,
            new PlayerDisconnected(disconnectedPeer, default));

        clientRegistry.SetupGet(m => m.LoadingPeers).Returns(new List<NetPeer> { disconnectedPeer });

        // Act
        handler.Handle_PlayerDisconnected(payload);

        // Assert
        var lockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(disconnectedPeer));
        Assert.False(lockMessage.IsLocked);
        Assert.Equal(0, lockMessage.LoadingPlayers);
    }

    [Fact]
    public void PlayerDisconnected_WhenOtherPeersStillLoading_RefreshesClientLock()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var handler = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);
        var disconnectedPeer = network.CreatePeer();
        var loadingPeer = network.CreatePeer();
        var payload = new MessagePayload<PlayerDisconnected>(
            this,
            new PlayerDisconnected(disconnectedPeer, default));

        clientRegistry.SetupGet(m => m.LoadingPeers).Returns(new List<NetPeer> { disconnectedPeer, loadingPeer });

        // Act
        handler.Handle_PlayerDisconnected(payload);

        // Assert
        var disconnectedPeerLockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(disconnectedPeer));
        Assert.True(disconnectedPeerLockMessage.IsLocked);
        Assert.Equal(1, disconnectedPeerLockMessage.LoadingPlayers);

        var loadingPeerLockMessage = Assert.Single(network.GetPeerMessagesFromType<NetworkTimeControlLockChanged>(loadingPeer));
        Assert.True(loadingPeerLockMessage.IsLocked);
        Assert.Equal(1, loadingPeerLockMessage.LoadingPlayers);
    }

    [Fact]
    public void PlayersLoadingPolicy_WhenPlayersLoading_ReturnsFalse()
    {
        // Arrange
        Func<bool>? policy = null;
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(true);
        clientRegistry.SetupGet(m => m.LoadingPeers).Returns(new List<NetPeer> { peer });
        timeControlInterface
            .Setup(m => m.AddUnpausePolicy(It.IsAny<Func<bool>>()))
            .Callback<Func<bool>>(p => policy = p);

        _ = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);

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
        var clientRegistry = new Mock<IClientRegistry>();
        var timeControlInterface = new Mock<ITimeControlInterface>();

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(false);
        timeControlInterface
            .Setup(m => m.AddUnpausePolicy(It.IsAny<Func<bool>>()))
            .Callback<Func<bool>>(p => policy = p);

        _ = new TimeHandler(broker, network, clientRegistry.Object, timeControlInterface.Object);

        // Act
        Assert.NotNull(policy);
        var result = policy();

        // Assert
        Assert.True(result);
    }
}
