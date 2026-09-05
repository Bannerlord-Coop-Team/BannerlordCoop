using Common.Messaging;
using Helpers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Locations.BoardGames.Messages;

public enum LordBoardGameReward
{
    None = 0,
    Relation = 1,
    Influence = 2,
    Renown = 3,
}

// Local events, published by the patch on the client.
public readonly struct BoardGameTavernResult : IEvent
{
    public readonly int BetAmount;
    public readonly BoardGameHelper.BoardGameState GameOver;

    public BoardGameTavernResult(int betAmount, BoardGameHelper.BoardGameState gameOver)
    {
        BetAmount = betAmount;
        GameOver = gameOver;
    }
}

public readonly struct BoardGameLordResult : IEvent
{
    public readonly Hero OpposingHero;
    public readonly BoardGameHelper.AIDifficulty Difficulty;
    public readonly LordBoardGameReward Reward;
    public readonly bool ExtraXp;

    public BoardGameLordResult(Hero opposingHero, BoardGameHelper.AIDifficulty difficulty, LordBoardGameReward reward, bool extraXp)
    {
        OpposingHero = opposingHero;
        Difficulty = difficulty;
        Reward = reward;
        ExtraXp = extraXp;
    }
}

// Network commands, client to server. The tavern payout hero is resolved
// server-side from the sending peer's player registry, so no hero or
// settlement ids travel on the wire.
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBoardGameTavernResult : ICommand
{
    [ProtoMember(1)]
    public readonly int BetAmount;
    [ProtoMember(2)]
    public readonly BoardGameHelper.BoardGameState GameOver;

    public NetworkBoardGameTavernResult(int betAmount, BoardGameHelper.BoardGameState gameOver)
    {
        BetAmount = betAmount;
        GameOver = gameOver;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBoardGameLordResult : ICommand
{
    [ProtoMember(1)]
    public readonly string OpposingHeroId;
    [ProtoMember(2)]
    public readonly BoardGameHelper.AIDifficulty Difficulty;
    [ProtoMember(3)]
    public readonly LordBoardGameReward Reward;
    [ProtoMember(4)]
    public readonly bool ExtraXp;

    public NetworkBoardGameLordResult(string opposingHeroId, BoardGameHelper.AIDifficulty difficulty, LordBoardGameReward reward, bool extraXp)
    {
        OpposingHeroId = opposingHeroId;
        Difficulty = difficulty;
        Reward = reward;
        ExtraXp = extraXp;
    }
}
