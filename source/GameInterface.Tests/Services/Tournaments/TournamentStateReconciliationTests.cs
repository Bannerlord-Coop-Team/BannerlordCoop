using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentStateReconciliationTests
{
    [Fact]
    public void GetStaleSessions_RemovesOnlySessionsAbsentFromAuthoritativeState()
    {
        TournamentSessionSnapshot keep = CreateSnapshot("keep", "town-a");
        TournamentSessionSnapshot stale = CreateSnapshot("stale", "town-b");

        TournamentSessionSnapshot[] result = TournamentStateReconciliation.GetStaleSessions(
            new[] { keep, stale },
            new[] { keep });

        Assert.Equal("stale", Assert.Single(result).SessionId);
    }

    [Fact]
    public void GetStaleSessions_EmptyAuthoritativeStateClearsReconnectCache()
    {
        TournamentSessionSnapshot[] result = TournamentStateReconciliation.GetStaleSessions(
            new[] { CreateSnapshot("stale", "town") },
            null);

        Assert.Single(result);
    }

    [Fact]
    public void ReconcileContestantScores_FillsMissingAndInvalidEntriesFromFrozenRoster()
    {
        var contestants = new[]
        {
            new TournamentContestantData("slot-a", "troop-a", 1, null, "A", false, false, false, null, 4),
            new TournamentContestantData("slot-b", "troop-b", 2, null, "B", false, false, false, null, 6)
        };
        TournamentSessionSnapshot snapshot = CreateSnapshot("session", "town", contestants);
        var candidate = new Dictionary<string, int>
        {
            ["slot-a"] = -1,
            ["extra"] = 99
        };

        IReadOnlyDictionary<string, int> result = TournamentStateReconciliation.ReconcileContestantScores(
            snapshot,
            candidate,
            out bool changed);

        Assert.True(changed);
        Assert.Equal(2, result.Count);
        Assert.Equal(4, result["slot-a"]);
        Assert.Equal(6, result["slot-b"]);
        Assert.DoesNotContain("extra", result.Keys);
    }

    private static TournamentSessionSnapshot CreateSnapshot(
        string sessionId,
        string townId,
        TournamentContestantData[] contestants = null)
    {
        return new TournamentSessionSnapshot(
            sessionId, sessionId, townId, "arena", "prize",
            TournamentSessionPhase.Preparation, 1, 0, null, null,
            new string[0], contestants ?? new TournamentContestantData[0], new string[0],
            new TournamentPlayerChoiceData[0], new TournamentRoundData[0],
            0, 0, 0, true, false, null);
    }
}
