using GameInterface.Services.SiegeEvents;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeKeepTroopSelectionTests
{
    // Vanilla's default hall model: 0.7 attackers per defender, at most 19.
    [Theory]
    [InlineData(30, new[] { 10, 3 }, new[] { 10, 3 })] // 19 places, more than both parties hold
    [InlineData(10, new[] { 10, 3 }, new[] { 3, 2 })]  // 7 places, 5 escorts shared in seat order
    [InlineData(10, new[] { 1, 10 }, new[] { 1, 4 })]  // a seat with no troops left stops taking places
    [InlineData(1, new[] { 10, 3 }, new[] { 0 })]      // one place seats only the first player
    [InlineData(0, new[] { 10 }, new[] { 0 })]         // vanilla always fields at least one attacker
    public void PlanSeats_SharesTheHallPlacesLeftAfterTheHeroes(int defenders, int[] escortsPerCandidate, int[] expected)
    {
        Assert.Equal(expected, SiegeKeepTroopSelection.PlanSeats(defenders, 0.7f, 19, escortsPerCandidate));
    }

    [Fact]
    public void SplitDefenders_FillsTheBudgetFromThePartiesInOrder()
    {
        var budget = new Dictionary<string, int> { ["recruit"] = 5, ["archer"] = 2 };
        var garrison = new List<(string, int)> { ("recruit", 3), ("archer", 4) };
        var lord = new List<(string, int)> { ("recruit", 4), ("archer", 1), ("cavalry", 6) };

        var split = SiegeKeepTroopSelection.SplitDefenders(budget,
            new IReadOnlyList<(string, int)>[] { garrison, lord });

        Assert.Equal(new Dictionary<string, int> { ["recruit"] = 3, ["archer"] = 2 }, split[0]);
        Assert.Equal(new Dictionary<string, int> { ["recruit"] = 2 }, split[1]);
    }
}
