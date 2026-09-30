using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Connection.Handlers;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Interfaces;
using LiteNetLib;
using Moq;
using System;
using System.Threading;
using Xunit;

namespace Coop.Tests.Client.Services.Connection.Handlers;

public class DisconnectHandlerTests
{
    [Fact]
    public void Timeout_ReturnsToMainMenuThenFinalizesWithActionablePopup()
    {
        const string expected =
            "Connection to the co-op server timed out.\nCheck your internet connection and try joining again.";

        RunDisconnect(DisconnectReason.Timeout, expected);
    }

    [Fact]
    public void NonTimeout_ReturnsToMainMenuThenFinalizesWithGenericPopup()
    {
        RunDisconnect(DisconnectReason.ConnectionFailed, "You have been Disconnected");
    }

    [Theory]
    [InlineData("JoinReplayAppliedTimeout", "Joining the campaign timed out while synchronizing.\nThe server stopped this join to keep the campaign responsive. Please try again.")]
    [InlineData("JoinReplayQueueLimit", "Joining the campaign stopped because its synchronization queue exceeded the safety limit.\nPlease try again.")]
    [InlineData("JoinCampaignEntryTimeout", "Joining the campaign timed out while loading the transferred save.\nThe server stopped this join to keep the campaign responsive. Please try again.")]
    [InlineData("ServerRestarting", "The server is restarting. Try again in a few minutes.")]
    [InlineData("UnknownReason", "You have been Disconnected")]
    [InlineData("", "You have been Disconnected")]
    public void ServerDisconnect_ReturnsToMainMenuThenExplainsJoinFailure(string serverReason, string expected)
    {
        RunDisconnect(DisconnectReason.RemoteConnectionClose, expected, serverReason);
    }

    [Fact]
    public void LeavingTheMap_SessionEndsWithoutAFinalize_ShowsTheServerReason()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        using var session = new CancellationTokenSource();
        // On the map EndGame destroys the game, and CoopMod.OnGameEnd ends the session with no finalize,
        // which wakes the blocking EndGame marshal.
        RunServerDisconnect(messageBroker, finalizer, session, () =>
        {
            session.Cancel();
            throw new OperationCanceledException("The game-thread session ended before the blocking Run action completed.");
        });

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        // That teardown already ended coop.
        Assert.Empty(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void SessionEndsWithoutAFinalizeBeforeTheHandlerFinalizes_ShowsTheServerReason()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        using var session = new CancellationTokenSource();
        RunServerDisconnect(messageBroker, finalizer, session, () => session.Cancel());

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Empty(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void CharacterCreation_StateFinalizesFirst_ShowsTheServerReasonOnce()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        using var session = new CancellationTokenSource();
        // In character creation EndGame returns, then the state's MainMenuEntered handler finalizes on the
        // next tick and its teardown ends the session before this handler's finalize reaches the game thread.
        RunServerDisconnect(messageBroker, finalizer, session, () =>
        {
            finalizer.Finalize("Client has been stopped");
            session.Cancel();
        });

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void CharacterCreation_HandlerFinalizesFirst_ShowsTheServerReasonOnce()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        using var session = new CancellationTokenSource();
        RunServerDisconnect(messageBroker, finalizer, session, () => { });

        // The state's MainMenuEntered handler on a later tick, before the queued teardown runs.
        finalizer.Finalize("Client has been stopped");

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
    }

    [Fact]
    public void FinalizeEndsTheSessionDuringTheReturn_ShowsTheServerReasonOnce()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        using var session = new CancellationTokenSource();
        RunServerDisconnect(messageBroker, finalizer, session, () =>
        {
            finalizer.Finalize("Client has been stopped");
            session.Cancel();
            throw new OperationCanceledException("The game-thread session ended before the blocking Run action completed.");
        });

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    private const string RestartingMessage = "The server is restarting. Try again in a few minutes.";

    private static NetworkDisconnected ServerDisconnect(string serverReason) =>
        new NetworkDisconnected(new DisconnectInfo { Reason = DisconnectReason.RemoteConnectionClose }, serverReason);

    private static void RunServerDisconnect(
        TestMessageBroker messageBroker, ICoopFinalizer finalizer, CancellationTokenSource session, Action goToMainMenu)
    {
        var gameState = new Mock<IGameStateInterface>();
        gameState.Setup(value => value.GoToMainMenu()).Callback(goToMainMenu);
        using var handler = new DisconnectHandler(messageBroker, finalizer, gameState.Object);

        // The network poller publishes the disconnect inside its session.
        using (GameThread.ActivateCancellation(session.Token))
        {
            messageBroker.Publish(handler, ServerDisconnect("ServerRestarting"));
        }
    }

    private static void RunDisconnect(DisconnectReason reason, string expectedMessage, string? serverReason = null)
    {
        var messageBroker = new TestMessageBroker();
        var finalizer = new Mock<ICoopFinalizer>(MockBehavior.Strict);
        var gameState = new Mock<IGameStateInterface>(MockBehavior.Strict);

        // GoToMainMenu must run before Finalize: Finalize queues the container teardown that
        // cancels the network session, which would drop GoToMainMenu's still-queued blocking
        // EndGame marshal and strand the player in a campaign with no coop container.
        var sequence = new MockSequence();
        finalizer.InSequence(sequence).Setup(value => value.SetCloseText(expectedMessage));
        gameState.InSequence(sequence).Setup(value => value.GoToMainMenu());
        finalizer.InSequence(sequence).Setup(value => value.Finalize(expectedMessage));
        using var handler = new DisconnectHandler(
            messageBroker,
            finalizer.Object,
            gameState.Object);

        messageBroker.Publish(
            handler,
            new NetworkDisconnected(new DisconnectInfo { Reason = reason }, serverReason));

        finalizer.Verify(value => value.SetCloseText(expectedMessage), Times.Once);
        gameState.Verify(value => value.GoToMainMenu(), Times.Once);
        finalizer.Verify(value => value.Finalize(expectedMessage), Times.Once);
    }
}
