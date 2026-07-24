using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class GameStateInterfaceTests
{
    [Fact]
    public void EndGame_DoesNotPublishMainMenuEnteredBeforeInitialStateActivation()
    {
        var messageBroker = new Mock<IMessageBroker>();
        bool endGameCalled = false;
        var gameStateInterface = new GameStateInterface(
            messageBroker.Object,
            () => endGameCalled = true);

        gameStateInterface.EndGame();

        Assert.True(endGameCalled);
        messageBroker.Verify(
            broker => broker.Publish(
                It.IsAny<object>(),
                It.IsAny<MainMenuEntered>()),
            Times.Never);
    }
}
