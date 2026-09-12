#if DEBUG
using GameInterface.Services.MobileParties.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class ClanLordMovementFixtureObservationTests
{
    private static ClanLordMovementFixture.Observation Create() =>
        new ClanLordMovementFixture.Observation("run-one", "player", "lord", "caravan", Point(24, 4));

    [Fact]
    public void ReleaseWithoutActualConversationCannotArmMovementVerification()
    {
        var observation = Create();
        Assert.NotNull(observation.ObserveDuring(false));
        Assert.NotNull(observation.ObserveRelease(Point(1, 2), 100));
        Assert.False(observation.During);
        Assert.False(observation.Released);
    }

    [Fact]
    public void ReleaseCapturesPostConversationPositionAndTime()
    {
        var observation = Create();
        Assert.Null(observation.ObserveDuring(true));
        Assert.Null(observation.ObserveRelease(Point(3, 4), 120));
        Assert.True(observation.Released);
        Assert.Equal(Point(3, 4), observation.Baseline);
        Assert.Equal(120, observation.Ticks);
    }

    [Fact]
    public void RepeatedReleaseKeepsFirstPostConversationBaseline()
    {
        var observation = Create();
        observation.ObserveDuring(true);
        observation.ObserveRelease(Point(3, 4), 120);
        observation.ObserveRelease(Point(7, 8), 150);
        Assert.Equal(Point(3, 4), observation.Baseline);
        Assert.Equal(120, observation.Ticks);
        Assert.NotNull(observation.ObserveDuring(true));
    }

    [Theory]
    [InlineData(1, "other-player")]
    [InlineData(2, "other-lord")]
    [InlineData(3, "other-caravan")]
    [InlineData(9, "old-run")]
    public void ObservationsCannotBeReusedForAnotherIdentity(int index, string value)
    {
        var observation = Create();
        string[] args = { "verify", "player", "lord", "caravan", "24", "4", "3", "4", "120", "run-one" };
        Assert.True(observation.Matches(args, 24, 4));
        args[index] = value;
        Assert.False(observation.Matches(args, 24, 4));
    }

    [Fact]
    public void ObservationsCannotChangeTheStagedDestination()
    {
        var observation = Create();
        string[] args = { "verify", "player", "lord", "caravan", "24", "4", "3", "4", "120", "run-one" };
        Assert.False(observation.Matches(args, 25, 4));
        Assert.False(observation.Matches(args, 24, 5));
    }

    private static CampaignVec2 Point(float x, float y) => new CampaignVec2(new Vec2(x, y), true);
}
#endif
