namespace GameInterface.Services.Tournaments.Data;

public sealed class TournamentBetQuote
{
    public int MaximumBet { get; }
    public float Odd { get; }

    public TournamentBetQuote(int maximumBet, float odd)
    {
        MaximumBet = maximumBet;
        Odd = odd;
    }
}
