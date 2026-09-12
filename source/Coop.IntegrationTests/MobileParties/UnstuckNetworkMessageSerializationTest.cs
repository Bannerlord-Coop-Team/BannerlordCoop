using GameInterface.Services.MobileParties.Messages.Unstuck;
using ProtoBuf;

namespace Coop.IntegrationTests.MobileParties;

public class UnstuckNetworkMessageSerializationTest
{
    [Fact]
    public void NetworkRequestPlayerUnstuck_RoundTrips()
    {
        var original = new NetworkRequestPlayerUnstuck("main_party", "Hero_Player");

        var copy = RoundTrip(original);

        Assert.Equal("main_party", copy.PartyId);
        Assert.Equal("Hero_Player", copy.HeroId);
    }

    [Fact]
    public void NetworkRequestPlayerUnstuck_RoundTrips_WithoutHero()
    {
        var original = new NetworkRequestPlayerUnstuck("main_party", null);

        var copy = RoundTrip(original);

        Assert.Equal("main_party", copy.PartyId);
        Assert.Null(copy.HeroId);
    }

    [Fact]
    public void NetworkPlayerUnstuckResult_RoundTrips()
    {
        var original = new NetworkPlayerUnstuckResult(
            "main_party",
            new[] { "Removed the party from settlement town_1.", "Warning: map event." });

        var copy = RoundTrip(original);

        Assert.Equal("main_party", copy.PartyId);
        Assert.NotNull(copy.Actions);
        Assert.Equal(2, copy.Actions.Length);
        Assert.Equal("Removed the party from settlement town_1.", copy.Actions[0]);
    }

    [Fact]
    public void NetworkPlayerUnstuckResult_EmptyActions_RoundTripsAsNullOrEmpty()
    {
        // Protobuf collapses empty repeated fields; the handler null-guards Actions, so either
        // shape is acceptable on the wire — this documents that the consumer must not assume
        // an instance.
        var original = new NetworkPlayerUnstuckResult("main_party", Array.Empty<string>());

        var copy = RoundTrip(original);

        Assert.Equal("main_party", copy.PartyId);
        Assert.True(copy.Actions == null || copy.Actions.Length == 0);
    }

    private static T RoundTrip<T>(T original)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
