using Common.Messaging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Handlers;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerDeletionHandlerTests
{
    [Fact]
    public void OfflineGameOverPublishesDeletionBeforeRemovingPlayerRegistration()
    {
        var player = new Player("owner", "hero", "party", "clan", "character");
        var broker = new Mock<IMessageBroker>();
        var players = new Mock<IPlayerManager>();
        var notified = false;
        var removed = false;
        broker.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<PlayerDeleting>()))
            .Callback<object, PlayerDeleting>((_, message) =>
            {
                Assert.False(removed);
                Assert.Same(player, message.Player);
                notified = true;
            });
        players.Setup(x => x.RemovePlayer(player)).Returns(() =>
        {
            Assert.True(notified);
            removed = true;
            return true;
        });
        using var handler = new PlayerDeletionHandler(broker.Object, new Mock<INetwork>().Object,
            new Mock<IObjectManager>().Object, players.Object, new Mock<ISiegeEventInterface>().Object,
            new CoopSessionProvider());

        handler.CompleteGameOver(player);

        Assert.True(removed);
        broker.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<PlayerDeleting>()), Times.Once);
    }
}
