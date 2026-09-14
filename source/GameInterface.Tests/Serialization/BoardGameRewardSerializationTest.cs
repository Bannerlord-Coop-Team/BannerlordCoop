using System.IO;
using GameInterface.Services.Locations.BoardGames.Messages;
using Helpers;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class BoardGameRewardSerializationTest
{
    [Fact]
    public void NetworkBoardGameTavernResult_RoundTrips()
    {
        var original = new NetworkBoardGameTavernResult(
            500,
            BoardGameHelper.BoardGameState.Win);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkBoardGameTavernResult>(stream);

        Assert.Equal(original.BetAmount, copy.BetAmount);
        Assert.Equal(original.GameOver, copy.GameOver);
    }

    [Fact]
    public void NetworkBoardGameLordResult_RoundTrips()
    {
        var original = new NetworkBoardGameLordResult(
            "hero_2",
            BoardGameHelper.AIDifficulty.Hard,
            LordBoardGameReward.Influence,
            extraXp: false);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkBoardGameLordResult>(stream);

        Assert.Equal(original.OpposingHeroId, copy.OpposingHeroId);
        Assert.Equal(original.Difficulty, copy.Difficulty);
        Assert.Equal(original.Reward, copy.Reward);
        Assert.Equal(original.ExtraXp, copy.ExtraXp);
    }
}
