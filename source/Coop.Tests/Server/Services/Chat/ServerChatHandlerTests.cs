using Common;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
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
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Server.Services.Chat;

public class ServerChatHandlerTests : IDisposable
{
    private readonly StubMessageBroker messageBroker = new();
    private readonly TestNetwork network = new();
    private readonly IPlayerManager playerManager;
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IChatPlayerNameResolver> playerNameResolver = new();
    private readonly Mock<IChatFactionMembership> factionMembership = new();
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
            playerNameResolver.Object,
            factionMembership.Object);
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
            playerNameResolver.Object,
            factionMembership.Object);

        participantHandler.SendParticipants(requesterPeer);

        var snapshot = Assert.Single(
            participantNetwork.GetPeerMessagesFromType<NetworkChatParticipants>(requesterPeer));
        Assert.Equal(new[] { "requester", "connected" }, snapshot.ControllerIds);
        Assert.Single(participantNetwork.ImmediateSends);
        Assert.IsType<NetworkChatParticipants>(participantNetwork.ImmediateSends[0].Payload);
    }

    [Fact]
    public void PlayerCampaignSynchronized_BroadcastsConnectedPlayersToAllPeers()
    {
        using var broker = new StubMessageBroker();
        using var broadcastNetwork = new TestNetwork();
        var joiningPeer = broadcastNetwork.CreatePeer();
        var otherPeer = broadcastNetwork.CreatePeer();
        var joining = Player("joining");
        var other = Player("other");
        var offline = Player("offline");
        var players = new Mock<IPlayerManager>();
        players.SetupGet(manager => manager.Players).Returns(new[] { joining, other, offline });
        players.Setup(manager => manager.IsConnected(joining)).Returns(true);
        players.Setup(manager => manager.IsConnected(other)).Returns(true);
        players.Setup(manager => manager.IsConnected(offline)).Returns(false);

        using var pushHandler = new ServerChatHandler(
            broker,
            broadcastNetwork,
            players.Object,
            playerNameResolver.Object,
            factionMembership.Object);

        broker.Publish(joiningPeer, new PlayerCampaignSynchronized(joiningPeer));
        DrainGameThread();

        foreach (var peer in new[] { joiningPeer, otherPeer })
        {
            var snapshot = Assert.Single(
                broadcastNetwork.GetPeerMessagesFromType<NetworkChatParticipants>(peer));
            Assert.Equal(new[] { "joining", "other" }, snapshot.ControllerIds);
        }
    }

    [Fact]
    public void ClanMessage_ReachesOnlySameClanPeers()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var clanmatePeer = CreatePeer("127.0.0.2");
        var outsiderPeer = CreatePeer("127.0.0.3");
        // Stale join-time ClanIds differ after JoinClan. Live Hero.Clan is the same object.
        var sender = Player("sender", clanId: "stale-sender-clan");
        var clanmate = Player("clanmate", clanId: "stale-clanmate-clan");
        var outsider = Player("outsider", clanId: "outsider-clan");
        ConfigurePlayers((senderPeer, sender), (clanmatePeer, clanmate), (outsiderPeer, outsider));

        var sharedClan = new Clan();
        var outsiderClan = new Clan();
        var liveClans = new Dictionary<string, Clan>(StringComparer.Ordinal)
        {
            [sender.ControllerId] = sharedClan,
            [clanmate.ControllerId] = sharedClan,
            [outsider.ControllerId] = outsiderClan,
        };
        SetupLiveClans(liveClans);

        handler.RouteMessage(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Clan, string.Empty, "for the clan"));

        foreach (var peer in new[] { senderPeer, clanmatePeer })
        {
            var message = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(peer));
            Assert.Equal(ChatChannel.Clan, message.Channel);
            Assert.Equal("for the clan", message.Text);
        }

        Assert.False(network.SentNetworkMessages.ContainsKey(outsiderPeer.Id));
    }

    [Fact]
    public void ClanMessage_WithoutClan_ReturnsSystemMessageOnlyToSender()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var otherPeer = CreatePeer("127.0.0.2");
        ConfigurePlayers((senderPeer, Player("sender")), (otherPeer, Player("other")));
        factionMembership
            .Setup(membership => membership.GetClan(It.IsAny<Player>()))
            .Returns((IFaction)null);

        handler.RouteMessage(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Clan, string.Empty, "hello?"));

        var rejection = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(senderPeer));
        Assert.Equal(ChatChannel.System, rejection.Channel);
        Assert.Equal("You are not in a clan.", rejection.Text);
        Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
    }

    [Fact]
    public void KingdomMessage_ReachesOnlySameKingdomPeers()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var allyPeer = CreatePeer("127.0.0.2");
        var outsiderPeer = CreatePeer("127.0.0.3");
        var sender = Player("sender", clanId: "clan-a");
        var ally = Player("ally", clanId: "clan-b");
        var outsider = Player("outsider", clanId: "clan-c");
        ConfigurePlayers((senderPeer, sender), (allyPeer, ally), (outsiderPeer, outsider));

        var sharedKingdom = new Kingdom();
        var liveKingdoms = new Dictionary<string, Kingdom>(StringComparer.Ordinal)
        {
            [sender.ControllerId] = sharedKingdom,
            [ally.ControllerId] = sharedKingdom,
        };
        SetupLiveKingdoms(liveKingdoms);

        handler.RouteMessage(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Kingdom, string.Empty, "for the realm"));

        foreach (var peer in new[] { senderPeer, allyPeer })
        {
            var message = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(peer));
            Assert.Equal(ChatChannel.Kingdom, message.Channel);
            Assert.Equal("for the realm", message.Text);
        }

        Assert.False(network.SentNetworkMessages.ContainsKey(outsiderPeer.Id));
    }

    [Fact]
    public void KingdomMessage_WithoutKingdom_ReturnsSystemMessageOnlyToSender()
    {
        var senderPeer = CreatePeer("127.0.0.1");
        var otherPeer = CreatePeer("127.0.0.2");
        ConfigurePlayers((senderPeer, Player("sender")), (otherPeer, Player("other")));
        factionMembership
            .Setup(membership => membership.GetKingdom(It.IsAny<Player>()))
            .Returns((IFaction)null);

        handler.RouteMessage(senderPeer,
            new NetworkSendChatMessage(ChatChannel.Kingdom, string.Empty, "hello?"));

        var rejection = Assert.Single(network.GetPeerMessagesFromType<NetworkChatMessage>(senderPeer));
        Assert.Equal(ChatChannel.System, rejection.Channel);
        Assert.Equal("You are not in a kingdom.", rejection.Text);
        Assert.False(network.SentNetworkMessages.ContainsKey(otherPeer.Id));
    }

    [Fact]
    public void PlayerDisconnected_BroadcastsConnectedPlayersToAllPeers()
    {
        using var broker = new StubMessageBroker();
        using var broadcastNetwork = new TestNetwork();
        var departedPeer = broadcastNetwork.CreatePeer();
        var otherPeer = broadcastNetwork.CreatePeer();
        var departed = Player("departed");
        var other = Player("other");
        var players = new Mock<IPlayerManager>();
        players.SetupGet(manager => manager.Players).Returns(new[] { departed, other });
        players.Setup(manager => manager.IsConnected(departed)).Returns(false);
        players.Setup(manager => manager.IsConnected(other)).Returns(true);

        using var pushHandler = new ServerChatHandler(
            broker,
            broadcastNetwork,
            players.Object,
            playerNameResolver.Object,
            factionMembership.Object);

        broker.Publish(departedPeer, new PlayerDisconnected(departedPeer, default));
        DrainGameThread();

        var snapshot = Assert.Single(
            broadcastNetwork.GetPeerMessagesFromType<NetworkChatParticipants>(otherPeer));
        Assert.Equal(new[] { "other" }, snapshot.ControllerIds);
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

    private void SetupLiveClans(IReadOnlyDictionary<string, Clan> liveClansByControllerId)
    {
        factionMembership
            .Setup(membership => membership.GetClan(It.IsAny<Player>()))
            .Returns((Player player) =>
                liveClansByControllerId.TryGetValue(player.ControllerId, out var clan)
                    ? clan
                    : null);
    }

    private void SetupLiveKingdoms(IReadOnlyDictionary<string, Kingdom> liveKingdomsByControllerId)
    {
        factionMembership
            .Setup(membership => membership.GetKingdom(It.IsAny<Player>()))
            .Returns((Player player) =>
                liveKingdomsByControllerId.TryGetValue(player.ControllerId, out var kingdom)
                    ? kingdom
                    : null);
    }

    private static Player Player(string controllerId, string clanId = "")
    {
        return new Player(controllerId, string.Empty, string.Empty, clanId ?? string.Empty, string.Empty);
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
