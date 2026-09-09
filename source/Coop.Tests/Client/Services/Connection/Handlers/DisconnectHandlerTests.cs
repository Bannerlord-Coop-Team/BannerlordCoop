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
    [InlineData("UnknownReason", "You have been Disconnected")]
    [InlineData("", "You have been Disconnected")]
    public void ServerDisconnect_ReturnsToMainMenuThenExplainsJoinFailure(string serverReason, string expected)
    {
        RunDisconnect(DisconnectReason.RemoteConnectionClose, expected, serverReason);
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
        gameState.InSequence(sequence).Setup(value => value.GoToMainMenu());
        finalizer.InSequence(sequence).Setup(value => value.Finalize(expectedMessage));
        using var handler = new DisconnectHandler(
            messageBroker,
            finalizer.Object,
            gameState.Object);

        messageBroker.Publish(
            handler,
            new NetworkDisconnected(new DisconnectInfo { Reason = reason }, serverReason));

        gameState.Verify(value => value.GoToMainMenu(), Times.Once);
        finalizer.Verify(value => value.Finalize(expectedMessage), Times.Once);
    }
}
