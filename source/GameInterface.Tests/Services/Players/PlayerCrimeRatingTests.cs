using GameInterface.Services.Players.Data;
using ProtoBuf;
using System.Text.Json;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerCrimeRatingTests
{
    [Fact]
    public void CrimeRatings_SaveAndNetworkRoundTripWithoutSharingPlayers()
    {
        var first = new Player("first", "hero1", "party1", "clan1", "character1");
        var second = new Player("second", "hero2", "party2", "clan2", "character2");
        first.CrimeRatings["Kingdom_empire_s"] = 60f;
        first.CrimeRatings["Clan_minor"] = 10f;
        var options = new JsonSerializerOptions { IncludeFields = true };
        var saved = JsonSerializer.Deserialize<Player>(JsonSerializer.Serialize(first, options), options);
        var transferred = Serializer.DeepClone(saved);
        Assert.Equal(60f, transferred.CrimeRatings["Kingdom_empire_s"]);
        Assert.Equal(10f, transferred.CrimeRatings["Clan_minor"]);
        Assert.Empty(second.CrimeRatings);
        transferred.CrimeRatings["Kingdom_empire_s"] = 20f;
        Assert.Equal(60f, first.CrimeRatings["Kingdom_empire_s"]);
    }

    [Fact]
    public void OldPlayerSaveWithoutCrimeRatingsStartsEmpty()
    {
        var player = JsonSerializer.Deserialize<Player>(
            "{\"ControllerId\":\"first\",\"HeroId\":\"hero\",\"MobilePartyId\":\"party\",\"ClanId\":\"clan\",\"CharacterObjectId\":\"character\"}",
            new JsonSerializerOptions { IncludeFields = true });
        Assert.Empty(player.CrimeRatings);
        Assert.Empty(Serializer.DeepClone(player).CrimeRatings);
    }
}
