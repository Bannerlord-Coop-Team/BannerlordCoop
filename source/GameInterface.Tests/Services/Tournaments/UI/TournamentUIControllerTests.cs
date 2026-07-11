using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentUIControllerTests
{
    [Fact]
    public void ArenaJoinRequest_CarriesTownSessionAndExpectedRevision()
    {
        using var controller = CreateController("player-a", out var sent);

        controller.RequestJoin("town-a", "session-a", 17);

        var request = Assert.IsType<NetworkRequestJoinTournament>(Assert.Single(sent));
        Assert.Equal("town-a", request.TownId);
        Assert.Equal("session-a", request.SessionId);
        Assert.Equal(17, request.ExpectedRevision);
    }

    [Fact]
    public void Tombstone_ClearsOnlyMatchingSessionAndTownCache()
    {
        using var controller = CreateController("player-a", out _);
        var oldSnapshot = CreateSnapshot("session-old", "town-a", 1);
        var newSnapshot = CreateSnapshot("session-new", "town-a", 1);
        Assert.True(controller.CacheSnapshot(oldSnapshot));
        Assert.True(controller.CacheSnapshot(newSnapshot));

        controller.RemoveSession("session-old", "town-a");

        Assert.False(controller.TryGetSession("session-old", out _));
        Assert.True(controller.TryGetTownSession("town-a", out var retained));
        Assert.Same(newSnapshot, retained);

        controller.RemoveSession("session-new", "town-a");

        Assert.False(controller.TryGetSession("session-new", out _));
        Assert.False(controller.TryGetTownSession("town-a", out _));
    }

    [Fact]
    public void StalePreparationLeave_RetriesAtCanonicalRevisionUntilAccepted()
    {
        using var controller = CreateController("player-a", out var sent);
        var revisionOne = CreateSnapshot("session-a", "town-a", 1, includeLocalContestant: true);
        controller.CacheSnapshot(revisionOne);

        controller.RequestLeavePreparation("town-a");
        var revisionTwo = CreateSnapshot("session-a", "town-a", 2, includeLocalContestant: true);
        controller.RetryPendingPreparationLeave(revisionTwo);
        var accepted = CreateSnapshot("session-a", "town-a", 3, includeLocalContestant: false);
        controller.RetryPendingPreparationLeave(accepted);
        controller.RetryPendingPreparationLeave(
            CreateSnapshot("session-a", "town-a", 4, includeLocalContestant: true));

        var requests = sent.Cast<NetworkRequestLeaveTournamentPreparation>().ToArray();
        Assert.Equal(2, requests.Length);
        Assert.Equal(1, requests[0].ExpectedRevision);
        Assert.Equal(2, requests[1].ExpectedRevision);
    }

    [Fact]
    public void StaleActiveLeave_RetriesAtCanonicalRevisionUntilMemberRemoved()
    {
        using var controller = CreateController("player-a", out var sent);
        var revisionOne = CreateSnapshot(
            "session-a",
            "town-a",
            1,
            TournamentSessionPhase.AwaitingChoices,
            true);

        controller.RequestLeaveActive(revisionOne);
        controller.RetryPendingActiveLeave(CreateSnapshot(
            "session-a",
            "town-a",
            2,
            TournamentSessionPhase.AwaitingChoices,
            true));
        controller.RetryPendingActiveLeave(CreateSnapshot(
            "session-a",
            "town-a",
            3,
            TournamentSessionPhase.AwaitingChoices,
            false));
        controller.RetryPendingActiveLeave(CreateSnapshot(
            "session-a",
            "town-a",
            4,
            TournamentSessionPhase.AwaitingChoices,
            true));

        var requests = sent.Cast<NetworkRequestLeaveActiveTournament>().ToArray();
        Assert.Equal(2, requests.Length);
        Assert.Equal(1, requests[0].ExpectedRevision);
        Assert.Equal(2, requests[1].ExpectedRevision);
    }

    [Fact]
    public void CompletedActiveLeave_RetriesUntilServerRemovesSession()
    {
        using var controller = CreateController("player-a", out var sent);
        var completed = CreateSnapshot(
            "session-a",
            "town-a",
            5,
            TournamentSessionPhase.Completed,
            true,
            isCompleted: true);

        controller.RequestLeaveActive(completed);
        controller.RetryPendingActiveLeave(completed);

        var requests = sent.Cast<NetworkRequestLeaveActiveTournament>().ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, request => Assert.Equal(5, request.ExpectedRevision));
    }

    [Fact]
    public void BetRequests_UseMonotonicPerSessionSequence()
    {
        using var controller = CreateController("player-a", out var sent);
        var snapshot = CreateSnapshot(
            "session-a",
            "town-a",
            8,
            TournamentSessionPhase.AwaitingChoices,
            true,
            "match-a");

        controller.RequestBet(snapshot, 10);
        controller.RequestBet(snapshot, 20);

        var requests = sent.Cast<NetworkRequestTournamentBet>().ToArray();
        Assert.Equal(2, requests.Length);
        Assert.Equal(1, requests[0].Sequence);
        Assert.Equal(2, requests[1].Sequence);
    }

    private static TournamentUIController CreateController(
        string controllerId,
        out List<IMessage> sent)
    {
        sent = new List<IMessage>();
        var captured = sent;
        var network = new Mock<INetwork>();
        network.Setup(value => value.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => captured.Add(message));
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(value => value.ControllerId).Returns(controllerId);

        return new TournamentUIController(
            new Mock<IMessageBroker>().Object,
            network.Object,
            new Mock<IObjectManager>().Object,
            controllerIdProvider.Object);
    }

    private static TournamentSessionSnapshot CreateSnapshot(
        string sessionId,
        string townId,
        long revision,
        TournamentSessionPhase phase = TournamentSessionPhase.Preparation,
        bool includeLocalContestant = false,
        string currentMatchId = null,
        bool isCompleted = false)
    {
        var contestants = includeLocalContestant
            ? new[]
            {
                new TournamentContestantData(
                    "slot-a",
                    "character-a",
                    1,
                    "player-a",
                    "Player A",
                    true,
                    false,
                    true,
                    "npc-a")
            }
            : Array.Empty<TournamentContestantData>();

        return new TournamentSessionSnapshot(
            sessionId,
            "mission-a",
            townId,
            "arena-a",
            "prize-a",
            phase,
            revision,
            1,
            currentMatchId,
            "host-a",
            Array.Empty<string>(),
            contestants,
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            Array.Empty<TournamentRoundData>(),
            0,
            0,
            0,
            true,
            isCompleted,
            null);
    }
}
