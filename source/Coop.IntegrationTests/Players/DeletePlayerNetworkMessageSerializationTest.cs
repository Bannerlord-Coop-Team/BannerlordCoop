using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using ProtoBuf;

namespace Coop.IntegrationTests.Players;

public class DeletePlayerNetworkMessageSerializationTest
{
    [Fact]
    public void NetworkRequestDeletePlayer_RoundTrips()
    {
        var original = new NetworkRequestDeletePlayer("Hero_Player");

        var copy = RoundTrip(original);

        Assert.Equal("Hero_Player", copy.HeroId);
    }

    [Fact]
    public void NetworkRequestDeletePlayer_RoundTrips_WithoutHero()
    {
        // The hero id is advisory; a client that cannot resolve its own hero sends null.
        var original = new NetworkRequestDeletePlayer(null);

        var copy = RoundTrip(original);

        Assert.Null(copy.HeroId);
    }

    [Fact]
    public void NetworkPlayerRemoved_RoundTrips()
    {
        var original = new NetworkPlayerRemoved("Controller_1", "Hero_Player");

        var copy = RoundTrip(original);

        Assert.Equal("Controller_1", copy.ControllerId);
        Assert.Equal("Hero_Player", copy.HeroId);
    }

    [Fact]
    public void NetworkPlayerCreationRolledBack_RoundTrips()
    {
        var player = new Player("Controller_1", "Hero_Player", "Party_Player", "Clan_Player", "Character_Player");
        var registrationIds = new[] { "Hero_Player", "TroopRoster_MemberRoster_Player" };
        var original = new NetworkPlayerCreationRolledBack(player, registrationIds);

        var copy = RoundTrip(original);

        Assert.Equal(player.ControllerId, copy.Player.ControllerId);
        Assert.Equal(player.HeroId, copy.Player.HeroId);
        Assert.Equal(player.MobilePartyId, copy.Player.MobilePartyId);
        Assert.Equal(player.ClanId, copy.Player.ClanId);
        Assert.Equal(player.CharacterObjectId, copy.Player.CharacterObjectId);
        Assert.Equal(registrationIds, copy.RegistrationIds);
    }

    [Fact]
    public void NetworkDeletePlayerDenied_RoundTrips()
    {
        var original = new NetworkDeletePlayerDenied("Cannot delete a player whose party is in a battle or siege.");

        var copy = RoundTrip(original);

        Assert.Equal("Cannot delete a player whose party is in a battle or siege.", copy.Reason);
    }

    private static T RoundTrip<T>(T original)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
