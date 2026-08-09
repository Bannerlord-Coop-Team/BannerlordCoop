using Common.Network;
using Common.Serialization;
using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatServiceTests
{
    [Fact]
    public void ParticipantSnapshot_RoundTripsControllerIds()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        byte[] payload = serializer.Serialize(new NetworkChatParticipants(new[] { "first", "second" }));

        var snapshot = Assert.IsType<NetworkChatParticipants>(serializer.Deserialize(payload));

        Assert.Equal(new[] { "first", "second" }, snapshot.ControllerIds);
    }

    [Fact]
    public void RequestParticipants_AsksServerForLiveMembership()
    {
        var network = new Mock<INetwork>();
        using var service = CreateService(network: network);

        service.RequestParticipants();

        network.Verify(value => value.SendAll(It.IsAny<NetworkRequestChatParticipants>()), Times.Once);
    }

    [Fact]
    public void ReceiveParticipants_ResolvesOnlyServerReportedPlayers()
    {
        var connected = Player("connected");
        var offline = Player("offline");
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { connected, offline });
        playerManager.Setup(manager => manager.TryGetPlayer("connected", out connected)).Returns(true);
        var playerNameResolver = new Mock<IChatPlayerNameResolver>();
        playerNameResolver.Setup(resolver => resolver.Resolve(connected)).Returns("Connected Hero");
        using var service = CreateService(
            playerManager: playerManager,
            playerNameResolver: playerNameResolver);

        service.ReceiveParticipants(new NetworkChatParticipants(new[] { "connected" }));

        playerNameResolver.Verify(resolver => resolver.Resolve(connected), Times.Once);
        playerNameResolver.Verify(resolver => resolver.Resolve(offline), Times.Never);
        playerManager.VerifyGet(manager => manager.Players, Times.Never);
    }

    private static ChatService CreateService(
        Mock<INetwork>? network = null,
        Mock<IPlayerManager>? playerManager = null,
        Mock<IChatPlayerNameResolver>? playerNameResolver = null)
    {
        network ??= new Mock<INetwork>();
        playerManager ??= new Mock<IPlayerManager>();
        playerNameResolver ??= new Mock<IChatPlayerNameResolver>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("local");

        return new ChatService(
            network.Object,
            playerManager.Object,
            playerNameResolver.Object,
            controllerIdProvider.Object);
    }

    private static Player Player(string controllerId)
    {
        return new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
