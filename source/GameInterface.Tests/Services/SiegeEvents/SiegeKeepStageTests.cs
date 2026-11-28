using GameInterface.Services.SiegeEvents;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeKeepStageTests
{
    // Vanilla's default hall model: 0.7 attackers per defender, at most 19.
    [Theory]
    [InlineData(30, new[] { 10, 3 }, new[] { 10, 3 })] // 19 places, more than both parties hold
    [InlineData(30, new[] { 20, 20 }, new[] { 9, 8 })] // 21 places capped to 19
    [InlineData(10, new[] { 10, 3 }, new[] { 3, 2 })]  // 7 places, 5 escorts shared in seat order
    [InlineData(10, new[] { 1, 10 }, new[] { 1, 4 })]  // a seat with no troops left stops taking places
    [InlineData(1, new[] { 10, 3 }, new[] { 0 })]      // one place seats only the first player
    public void PlanSeats_SharesTheHallPlacesLeftAfterTheHeroes(int defenders, int[] escortsPerCandidate, int[] expected)
    {
        Assert.Equal(expected, SiegeKeepStage.PlanSeats(defenders, 0.7f, 19, escortsPerCandidate));
    }
}
