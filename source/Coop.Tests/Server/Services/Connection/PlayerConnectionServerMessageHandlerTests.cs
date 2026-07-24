using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using Xunit;

namespace Coop.Tests.Server.Services.Connection;

public class PlayerConnectionServerMessageHandlerTests
{
    private const string LoadingText = "2 player(s) are currently joining the game";
    private const string AllPlayersConnectedText = "All players connected";

    [Fact]
    public void LoadingPlayersChanged_WhenPlayersLoading_SendsJoiningMessage()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var handler = new PlayerConnectionServerMessageHandler(broker, network);

        handler.Handle_LoadingPlayersChanged(
            new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(2)));

        var localMessage = Assert.Single(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.Equal(LoadingText, localMessage.Text);

        var connectedPeerMessage = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(connectedPeer));
        Assert.Equal(localMessage.Text, connectedPeerMessage.Text);
    }

    [Fact]
    public void LoadingPlayersChanged_WhenNoPlayersLoading_SendsAllPlayersConnectedMessage()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var connectedPeer = network.CreatePeer();
        var handler = new PlayerConnectionServerMessageHandler(broker, network);

        handler.Handle_LoadingPlayersChanged(
            new MessagePayload<LoadingPlayersChanged>(this, new LoadingPlayersChanged(0)));

        var localMessage = Assert.Single(broker.GetMessagesFromType<SendInformationMessage>());
        Assert.Equal(AllPlayersConnectedText, localMessage.Text);

        var connectedPeerMessage = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(connectedPeer));
        Assert.Equal(localMessage.Text, connectedPeerMessage.Text);
    }
}
