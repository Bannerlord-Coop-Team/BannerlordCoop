using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using System;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

/// <summary>Checks simulated result encoding without loading a campaign.</summary>
public class TournamentGameInterfaceTests
{
    [Fact]
    public void BuildSimulatedMatchResult_PreservesWinnerOrderAndScoresEveryTeam()
    {
        var teams = new[]
        {
            new TournamentTeamData("left", new[] { "left-1", "left-2" }, 9, false, 0, null),
            new TournamentTeamData("middle", new[] { "middle-1", "middle-2" }, 9, false, 0, null),
            new TournamentTeamData("right", new[] { "right-1", "right-2" }, 9, false, 0, null)
        };
        var match = new TournamentMatchData("match", "round", 0, 2, 4, teams, Array.Empty<string>());
        var snapshot = new TournamentSessionSnapshot(
            "session", "mission", "town", "arena", null, TournamentSessionPhase.SimulatingMatch,
            5, 2, match.MatchId, null, null, null, null, null, null, 0, 0, 0, false, false, null);
        var gameInterface = new TournamentGameInterface(null, null);

        var input = new TournamentSimulationInput(match, teams, WinnerTeamCount: 2, WinnerParticipantCount: 4);
        var result = gameInterface.BuildSimulatedMatchResult(snapshot, input, new[] { "right", "left" }, 7);

        Assert.Equal(new[] { "right", "left" }, result.WinnerTeamIds);
        Assert.Equal(new[] { "right-1", "right-2", "left-1", "left-2" }, result.WinnerSlotIds);
        Assert.Equal(new[] { ("left", 1), ("middle", 0), ("right", 2) },
            result.TeamScores.Select(score => (score.TeamId, score.Score)));
    }
}
