using Common;
using Coop.Core.Server.Services.Chat.Handlers;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using Serilog;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Chat;

public class ServerChatHandlerTests : IDisposable
{
    private readonly StubMessageBroker messageBroker = new();
    private readonly TestNetwork network = new();
    private readonly IPlayerManager playerManager;
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IChatPlayerNameResolver> playerNameResolver = new();
    private readonly ServerChatHandler handler;

    public ServerChatHandlerTests()
    {
        playerManager = new PlayerManager(
            Mock.Of<ILogger>(),
            objectManager.Object,
            Mock.Of<IControllerIdProvider>());
        playerNameResolver
            .Setup(resolver => resolver.Resolve(It.IsAny<Player>()))
            .Returns((Player player) => player.ControllerId);
        handler = new ServerChatHandler(
            messageBroker,
            network,
            playerManager,
            playerNameResolver.Object);
    }

    [Fact]
    public void GlobalMessage_UsesPeerIdentityAndReachesEveryRegisteredPeer()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var otherPeer = CreatePeer("127.0.0.2");
        var sender = Player("sender");
        var other = Player("other");
        ConfigurePlayers((senderPeer, sender), (otherPeer, other));

        messageBroker.Publish(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Global, "ignored", "  hello\nworld  "));
        DrainGameThread();

        foreach (var peer in new[] { senderPeer, otherPeer })
        {
            var message = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(peer));
            Assert.Equal(ChatChannel.Global, message.Channel);
            Assert.Equal("sender", message.SenderControllerId);
            Assert.Equal("sender", message.SenderName);
            Assert.Equal("hello world", message.Text);
        }

        Assert.Equal(2, network.ImmediateSends.Count(send => send.Payload is NetworkChatMessage));
    }

    [Fact]
    public void DirectMessage_ReachesOnlySenderAndRecipient()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var recipientPeer = CreatePeer("127.0.0.2");
        var observerPeer = CreatePeer("127.0.0.3");
        var sender = Player("sender");
        var recipient = Player("recipient");
        var observer = Player("observer");
        ConfigurePlayers(
            (senderPeer, sender),
            (recipientPeer, recipient),
            (observerPeer, observer));

        handler.RouteMessage(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Direct, "recipient", "secret"));

        foreach (var peer in new[] { senderPeer, recipientPeer })
        {
            var message = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(peer));
            Assert.Equal(ChatChannel.Direct, message.Channel);
            Assert.Equal("sender", message.SenderControllerId);
            Assert.Equal("recipient", message.RecipientControllerId);
            Assert.Equal("secret", message.Text);
        }

        Assert.False(network.SentNetworkMessages.ContainsKey(observerPeer.Id));
        Assert.Equal(2, network.ImmediateSends.Count(send => send.Payload is NetworkChatMessage));
    }

    [Fact]
    public void DirectMessage_ToDisconnectedPlayer_ReturnsSystemMessageOnlyToSender()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var sender = Player("sender");
        var disconnected = Player("disconnected");
        ConfigurePlayers((senderPeer, sender));
        playerManager.AddPlayer(disconnected);

        handler.RouteMessage(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Direct, "disconnected", "hello?"));

        var rejection = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(senderPeer));
        Assert.Equal(ChatChannel.System, rejection.Channel);
        Assert.Equal("disconnected", rejection.RecipientControllerId);
        Assert.Equal("That player is not currently connected.", rejection.Text);
        Assert.Single(network.ImmediateSends);
    }

    [Fact]
    public void OversizedMessage_IsRejectedWithoutBroadcastingIt()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var otherPeer = CreatePeer("127.0.0.2");
        ConfigurePlayers((senderPeer, Player("sender")), (otherPeer, Player("other")));

        handler.RouteMessage(senderPeer, new NetworkSendChatMessage(
            ChatChannel.Global,
            string.Empty,
            new string('x', ChatMessageLimits.MaxMessageLength + 1)));

        var rejection = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(senderPeer));
        Assert.Equal(ChatChannel.System, rejection.Channel);
        Assert.Contains(ChatMessageLimits.MaxMessageLength.ToString(), rejection.Text);
        Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
        Assert.Single(network.ImmediateSends);
    }

    [Fact]
    public void ParticipantRequest_ReturnsOnlyLiveSessionMembersImmediately()
    {
        using var broker = new StubMessageBroker();
        using var participantNetwork = new TestNetwork();
        var requesterPeer = participantNetwork.CreatePeer();
        var requester = Player("requester");
        var connected = Player("connected");
        var disconnected = Player("disconnected");
        var players = new Mock<IPlayerManager>();
        Player registeredRequester = requester;
        players.Setup(manager => manager.TryGetPlayer(requesterPeer, out registeredRequester)).Returns(true);
        players.SetupGet(manager => manager.Players).Returns(new[] { requester, connected, disconnected });
        players.Setup(manager => manager.IsConnected(requester)).Returns(true);
        players.Setup(manager => manager.IsConnected(connected)).Returns(true);
        players.Setup(manager => manager.IsConnected(disconnected)).Returns(false);

        using var participantHandler = new ServerChatHandler(
            broker,
            participantNetwork,
            players.Object,
            playerNameResolver.Object);

        participantHandler.SendParticipants(requesterPeer);

        var snapshot = Assert.Single(
            participantNetwork.GetPeerMessagesFromType<NetworkChatParticipants>(requesterPeer));
        Assert.Equal(new[] { "requester", "connected" }, snapshot.ControllerIds);
        Assert.Single(participantNetwork.ImmediateSends);
        Assert.IsType<NetworkChatParticipants>(participantNetwork.ImmediateSends[0].Payload);
    }

    public void Dispose()
    {
        handler.Dispose();
        network.Dispose();
        messageBroker.Dispose();
    }

    private void ConfigurePlayers(params (NetPeer Peer, Player Player)[] registrations)
    {
        foreach (var registration in registrations)
        {
            playerManager.AddPlayer(registration.Player);
            playerManager.SetPeer(registration.Player.ControllerId, registration.Peer);
        }
    }

    private static Player Player(string controllerId)
    {
        return new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private NetPeer CreatePeer(string ipAddress)
    {
        var peer = network.CreatePeer();
        peer.Setup(peer.Id, ipAddress);
        return peer;
    }

    private static void DrainGameThread()
    {
        GameThread.Run(() => { }, blocking: true, label: nameof(ServerChatHandlerTests));
    }
}
