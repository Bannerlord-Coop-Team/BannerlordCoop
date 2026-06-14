using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Connection;

public class PlayerConnectionServerMessageHandlerTests
{
    private const string LoadingText = "Time controls disabled, 2 player(s) are currently joining the game";
    private const string UnpauseReadyText = "All players connected, game can now be un-paused";

    [Fact]
    public void LoadingPlayersChanged_WhenPlayersLoading_SendsTimeControlsDisabledMessage()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var handler = new PlayerConnectionServerMessageHandler(broker, network);
        var payload = new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(2));

        // Act
        handler.Handle_LoadingPlayersChanged(payload);

        // Assert
        var localMessage = Assert.Single(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.Equal(LoadingText, localMessage.Text);

        var connectedPeerMessage = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(connectedPeer));
        Assert.Equal(localMessage.Text, connectedPeerMessage.Text);
    }

    [Fact]
    public void LoadingPlayersChanged_WhenNoPlayersLoading_SendsUnpauseReadyMessage()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var handler = new PlayerConnectionServerMessageHandler(broker, network);
        var payload = new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(0));

        // Act
        handler.Handle_LoadingPlayersChanged(payload);

        // Assert
        var localMessage = Assert.Single(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.Equal(UnpauseReadyText, localMessage.Text);

        var connectedPeerMessage = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(connectedPeer));
        Assert.Equal(localMessage.Text, connectedPeerMessage.Text);
    }

    [Fact]
    public void TimeSpeedChangeAttempted_WhilePlayersLoading_RemindsControlsAreDisabled()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var handler = new PlayerConnectionServerMessageHandler(broker, network);
        handler.Handle_LoadingPlayersChanged(
            new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(2)));

        // Act
        handler.Handle_TimeSpeedChangeAttempted(
            new MessagePayload<TimeSpeedChangedAttempted>(this, new TimeSpeedChangedAttempted(TimeControlEnum.Play_1x)));

        // Assert: the loading announcement plus the attempt reminder, both with the loading text.
        var messages = broker.GetMessagesFromType<SendInformationMessage>().ToList();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(LoadingText, m.Text));
    }

    [Fact]
    public void TimeSpeedChangeAttempted_WhenNoPlayersLoading_SendsNothing()
    {
        // Arrange
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var handler = new PlayerConnectionServerMessageHandler(broker, network);

        // Act
        handler.Handle_TimeSpeedChangeAttempted(
            new MessagePayload<TimeSpeedChangedAttempted>(this, new TimeSpeedChangedAttempted(TimeControlEnum.Play_1x)));

        // Assert
        Assert.Empty(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.False(network.SentNetworkMessages.ContainsKey(connectedPeer.Id));
    }
}
