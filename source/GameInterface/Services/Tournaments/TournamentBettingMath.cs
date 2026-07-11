using System;

namespace GameInterface.Services.Tournaments;

public static class TournamentBettingMath
{
    public const int BaseMaximumBet = 150;

    public static int CalculateMaximumBet(bool hasDeepPockets, float primaryBonus)
    {
        if (!hasDeepPockets)
            return BaseMaximumBet;
        return checked(BaseMaximumBet * (int)primaryBonus);
    }

    public static float CalculateOdd(
        float heroPower,
        float playerTeamPower,
        float currentMatchOpponentPower,
        float totalRoundPower)
    {
        float ownPower = heroPower + playerTeamPower;
        float currentMatchPower = ownPower + currentMatchOpponentPower;
        float otherMatchPower = Math.Max(
            0f,
            totalRoundPower - playerTeamPower - currentMatchOpponentPower);
        float adjustedRoundPower = ownPower + (0.5f * otherMatchPower);
        if (ownPower <= 0f || currentMatchPower <= 0f || adjustedRoundPower <= 0f)
            return 1.1f;

        float winFactor = (ownPower / currentMatchPower) / (ownPower / adjustedRoundPower);
        if (winFactor <= 0f)
            return 4f;

        float rawOdd = (float)Math.Pow(1f / (winFactor * winFactor), 0.75f);
        float odd = Math.Max(1.1f, Math.Min(4f, rawOdd));
        return (int)(odd * 10f) / 10f;
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
