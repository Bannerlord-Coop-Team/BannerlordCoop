using MathF = TaleWorlds.Library.MathF;

namespace GameInterface.Services.Tournaments;

public static class TournamentBettingMath
{
    public const int BaseMaximumBet = 150;

    public static int CalculateMaximumBet(bool hasDeepPockets, float primaryBonus)
    {
        if (!hasDeepPockets)
            return BaseMaximumBet;
        return BaseMaximumBet * (int)primaryBonus;
    }

    public static float CalculateOdd(
        float heroPower,
        float playerTeamPower,
        float currentMatchOpponentPower,
        float totalRoundPower)
    {
        float ownPower = heroPower + playerTeamPower;
        float currentMatchPower = ownPower + currentMatchOpponentPower;
        float otherMatchPower = totalRoundPower - playerTeamPower - currentMatchOpponentPower;
        float adjustedRoundPower = ownPower + (0.5f * otherMatchPower);
        float winFactor = (ownPower / currentMatchPower) / (heroPower / adjustedRoundPower);
        float rawOdd = MathF.Pow(1f / (winFactor * winFactor), 0.75f);
        float odd = MathF.Clamp(rawOdd, 1.1f, 4f);
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
