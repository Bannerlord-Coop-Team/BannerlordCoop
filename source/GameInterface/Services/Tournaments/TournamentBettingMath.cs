namespace GameInterface.Services.Tournaments;

public static class TournamentBettingMath
{
    public static int CalculateExpectedPayout(int stake, float odd)
        => (int)(stake * odd);

    public static bool IsValidStake(int amount, int currentBet, int maximumBet, int availableGold)
    {
        return amount > 0 &&
            currentBet >= 0 &&
            currentBet <= maximumBet &&
            amount <= maximumBet - currentBet &&
            amount <= availableGold;
    }
}
