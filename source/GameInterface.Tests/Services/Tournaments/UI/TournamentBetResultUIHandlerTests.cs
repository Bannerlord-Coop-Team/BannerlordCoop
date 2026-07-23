using GameInterface.Services.Tournaments.Messages;
using Common.Messaging;
using GameInterface.Services.Tournaments.UI;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentBetResultUIHandlerTests
{
    [Fact]
    public void AcceptedBetMessage_UsesCanonicalCumulativeLedger()
    {
        var result = new NetworkTournamentBetResult(
            "session-a",
            7,
            2,
            "match-a",
            true,
            null,
            140,
            60,
            250,
            false);

        string message = TournamentBetResultUIHandler.GetMessage(result);

        Assert.Contains("140", message);
        Assert.Contains("250", message);
    }

    [Fact]
    public void SettlementMessage_UsesCanonicalSettlementReason()
    {
        var result = new NetworkTournamentBetResult(
            "session-a",
            8,
            3,
            "match-a",
            true,
            "Tournament bet forfeited",
            0,
            0,
            0,
            true);

        Assert.Equal(
            "Tournament bet forfeited",
            TournamentBetResultUIHandler.GetMessage(result));
    }

    [Fact]
    public void DuplicateOrOlderSequence_DoesNotRepeatNotification()
    {
        var handler = new TournamentBetResultUIHandler(new Mock<IMessageBroker>().Object);
        var first = new NetworkTournamentBetResult(
            "session-a",
            7,
            2,
            "match-a",
            true,
            null,
            100,
            60,
            180,
            false);
        var duplicate = new NetworkTournamentBetResult(
            "session-a",
            8,
            2,
            "match-a",
            true,
            null,
            150,
            100,
            260,
            false);

        Assert.True(handler.TryAccept(first));
        Assert.False(handler.TryAccept(duplicate));
    }}
