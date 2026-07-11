using GameInterface.Services.Tournaments;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentBettingMathTests
{
    [Fact]
    public void CalculateOdd_UsesCurrentOpponentsAndHalfPowerFromOtherMatches()
    {
        float odd = TournamentBettingMath.CalculateOdd(
            heroPower: 40f,
            playerTeamPower: 20f,
            currentMatchOpponentPower: 60f,
            totalRoundPower: 100f);

        Assert.Equal(1.2f, odd);
    }

    [Theory]
    [InlineData(100f, 0f, 1f, 1f, 1.1f)]
    [InlineData(1f, 0f, 100f, 100f, 4f)]
    public void CalculateOdd_ClampsToNativeBounds(
        float heroPower,
        float playerTeamPower,
        float opponentPower,
        float totalPower,
        float expected)
    {
        Assert.Equal(expected, TournamentBettingMath.CalculateOdd(
            heroPower,
            playerTeamPower,
            opponentPower,
            totalPower));
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
