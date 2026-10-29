using GameInterface.Services.Tournaments;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentBettingMathTests
{
    [Theory]
    [InlineData(150, 4f, 600)]
    [InlineData(150, 1.1f, 165)]
    public void CalculateExpectedPayout_MatchesNativeTournamentBehavior(
        int stake,
        float odd,
        int expected)
    {
        Assert.Equal(expected, TournamentBettingMath.CalculateExpectedPayout(stake, odd));
    }

    [Theory]
    [InlineData(150, 1f, 150)]
    [InlineData(150, 4f, 600)]
    [InlineData(300, 2.5f, 750)]
    [InlineData(150, 0.7f, 105)]
    [InlineData(150, 0f, 0)]
    [InlineData(150, -1f, 150)]
    [InlineData(150, 1e9f, int.MaxValue)]
    public void ScaleMaximumBet_AppliesTheServerMultiplier(
        int maximumBet,
        float multiplier,
        int expected)
    {
        Assert.Equal(expected, TournamentBettingMath.ScaleMaximumBet(maximumBet, multiplier));
    }

    [Theory]
    [InlineData(-1, 0, 150, 1000)]
    [InlineData(int.MaxValue, 1, 150, int.MaxValue)]
    [InlineData(51, 100, 150, 1000)]
    [InlineData(50, 100, 150, 49)]
    public void IsValidStake_RejectsNegativeOversizedAndOverCapRequests(
        int amount,
        int currentBet,
        int maximumBet,
        int availableGold)
    {
        Assert.False(TournamentBettingMath.IsValidStake(amount, currentBet, maximumBet, availableGold));
    }
}
