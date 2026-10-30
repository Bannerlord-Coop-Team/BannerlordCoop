using System;

namespace GameInterface.Services.Tournaments;

public static class TournamentBettingMath
{
    public static int CalculateExpectedPayout(int stake, float odd)
        => (int)(stake * odd);

    public static int ScaleMaximumBet(int maximumBet, float multiplier)
    {
        // Negative multipliers fall back to vanilla, like the other mod-config multipliers.
        if (multiplier < 0) return maximumBet;

        return (int)Math.Min(int.MaxValue, Math.Round(maximumBet * (double)multiplier));
    }

    public static bool IsValidStake(int amount, int currentBet, int maximumBet, int availableGold)
    {
        return amount > 0 &&
            currentBet >= 0 &&
            currentBet <= maximumBet &&
            amount <= maximumBet - currentBet &&
            amount <= availableGold;
    }
}
