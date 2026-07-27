using Common.Network.Messages;
using Coop.Core.Server.Services.Players.Handlers;
using Coop.Tests.Mocks;
using Coop.Tests.Stubs;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Server.Services.Players;

public class DisconnectedPlayersServerHandlerTests
{
    private static Mock<IPlayerManager> CreatePlayerManager(
    IReadOnlyCollection<Player> players,
    ISet<Player> connected)
    {
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(players);
        playerManager.Setup(manager => manager.IsConnected(It.IsAny<Player>()))
            .Returns<Player>(player => connected.Contains(player));
        return playerManager;
    }

    [Fact]
    public void PlayerDisconnected_WhenOnePlayerStillConnected_DoesNotPause()
    {
        var broker = new StubMessageBroker();
        var network = new TestNetwork();
        var timeControlInterface = new Mock<ITimeControlInterface>();
        var connectedPlayer1 = new Player("contr1", "hero1", "party1", "clan1", "char1");
        var connectedPlayer2 = new Player("contr2", "hero2", "party2", "clan2", "char1");
        var connected = new HashSet<Player> { connectedPlayer1, connectedPlayer2 };
        var playerManager = CreatePlayerManager(
            new[] { connectedPlayer1, connectedPlayer2 },
            connected);
        
        var handler = new DisconnectedPlayersServerHandler(broker, network, playerManager.Object, timeControlInterface.Object);

        connected.Remove(connectedPlayer1);
        broker.Publish(this, new PlayerDisconnected(null!, default));
        
        timeControlInterface.Verify(m => m.ServerSetTimeControl(TimeControlEnum.Pause), Times.Never);

        connected.Remove(connectedPlayer2);
        broker.Publish(this, new PlayerDisconnected(null!, default));
        timeControlInterface.Verify(m => m.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once);
    }
}
