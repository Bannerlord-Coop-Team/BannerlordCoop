using System;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentMissionUIContextTests
{
    [Fact]
    public void Clear_OnlyClearsMatchingSession()
    {
        var context = new TournamentMissionUIContext();
        var snapshot = CreateSnapshot("session-a");
        context.Set(snapshot);

        context.Clear("session-b");

        Assert.True(context.TryGet(out var retained));
        Assert.Same(snapshot, retained);

        context.Clear("session-a");

        Assert.False(context.TryGet(out _));
    }

    private static TournamentSessionSnapshot CreateSnapshot(string sessionId)
        => new(
            sessionId,
            "mission-a",
            "town-a",
            "arena-a",
            "prize-a",
            TournamentSessionPhase.AwaitingChoices,
            1,
            1,
            "match-a",
            "host-a",
            Array.Empty<string>(),
            Array.Empty<TournamentContestantData>(),
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            Array.Empty<TournamentRoundData>(),
            0,
            0,
            0,
            true,
            false,
            null);
}
