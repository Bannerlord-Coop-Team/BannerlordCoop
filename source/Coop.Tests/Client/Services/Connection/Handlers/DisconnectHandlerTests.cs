using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Connection.Handlers;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using LiteNetLib;
using Moq;
using Xunit;

namespace Coop.Tests.Client.Services.Connection.Handlers;

public class DisconnectHandlerTests
{
    [Fact]
    public void Timeout_FinalizesWithActionablePopupBeforeContainerTeardown()
    {
        const string expected =
            "Connection to the co-op server timed out.\nCheck your internet connection and try joining again.";

        RunDisconnect(DisconnectReason.Timeout, expected);
    }

    [Fact]
    public void NonTimeout_FinalizesWithGenericPopupBeforeContainerTeardown()
    {
        RunDisconnect(DisconnectReason.ConnectionFailed, "You have been Disconnected");
    }

    private static void RunDisconnect(DisconnectReason reason, string expectedMessage)
    {
        var messageBroker = new TestMessageBroker();
        var finalizer = new Mock<ICoopFinalizer>(MockBehavior.Strict);
        var gameState = new Mock<IGameStateInterface>(MockBehavior.Strict);
        var sequence = new MockSequence();
        finalizer.InSequence(sequence).Setup(value => value.Finalize(expectedMessage));
        gameState.InSequence(sequence).Setup(value => value.GoToMainMenu());
        using var handler = new DisconnectHandler(
            messageBroker,
            finalizer.Object,
            gameState.Object);

        messageBroker.Publish(
            handler,
            new NetworkDisconnected(new DisconnectInfo { Reason = reason }));

        gameState.Verify(value => value.GoToMainMenu(), Times.Once);
        finalizer.Verify(value => value.Finalize(expectedMessage), Times.Once);
    }
}
