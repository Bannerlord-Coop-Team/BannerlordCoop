using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Core.Server.Services.Connection.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Connection;

public class PlayerConnectionServerMessageHandlerTests
{
    [Fact]
    public void PlayerLoading_WhenPlayerIsLoading_SendsTimeControlsDisabledMessage()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var loadingPeer = network.CreatePeer();
        var clientRegistry = new Mock<IClientRegistry>();
        var handler = new PlayerConnectionServerMessageHandler(broker, clientRegistry.Object, network);
        var payload = new MessagePayload<PlayerLoading>(this, new PlayerLoading());

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(true);
        clientRegistry.SetupGet(m => m.LoadingPeers).Returns(new List<NetPeer> { loadingPeer });

        // Act
        handler.Handle_PlayerLoading(payload);

        // Assert
        var localMessage = Assert.Single(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.Equal("Time controls disabled, 1 player(s) are currently joining the game", localMessage.Text);

        var connectedPeerMessage = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(connectedPeer));
        Assert.Equal(localMessage.Text, connectedPeerMessage.Text);

        var loadingPeerMessage = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(loadingPeer));
        Assert.Equal(localMessage.Text, loadingPeerMessage.Text);
    }

    [Fact]
    public void PlayerLoading_WhenNoPlayerIsLoading_DoesNotSendTimeControlsDisabledMessage()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var clientRegistry = new Mock<IClientRegistry>();
        var handler = new PlayerConnectionServerMessageHandler(broker, clientRegistry.Object, network);
        var payload = new MessagePayload<PlayerLoading>(this, new PlayerLoading());

        clientRegistry.SetupGet(m => m.PlayersLoading).Returns(false);

        // Act
        handler.Handle_PlayerLoading(payload);

        // Assert
        Assert.Empty(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.False(network.SentNetworkMessages.ContainsKey(connectedPeer.Id));
    }
}
