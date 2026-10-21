using Missions.Agents.Handlers;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class NavalDeckPosesTests
{
    private static readonly Vec2 Min = new Vec2(-4f, -15f);
    private static readonly Vec2 Max = new Vec2(4f, 15f);

    // A deck agent, or one on a plank reaching past the side, keeps a hull-local pose.
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(4f + NavalDeckPoses.FootprintMarginMetres, 15f)]
    [InlineData(-14f, -20f)]
    public void IsOverFootprint_AcceptsAPoseOnTheHullOrWithinTheMargin(float x, float y)
    {
        Assert.True(NavalDeckPoses.IsOverFootprint(new Vec3(x, y, -4.6f), Min, Max));
    }

    // R43: corsair 4f4d9701 sent deckLocal (-334, 328, -4.6) on 1efe6d0f, about 470 m off that hull.
    [Theory]
    [InlineData(-334.3f, 328.4f)]
    [InlineData(4.1f + NavalDeckPoses.FootprintMarginMetres, 0f)]
    [InlineData(0f, -15.1f - NavalDeckPoses.FootprintMarginMetres)]
    public void IsOverFootprint_RefusesAPoseFarOffTheHull(float x, float y)
    {
        Assert.False(NavalDeckPoses.IsOverFootprint(new Vec3(x, y, -4.6f), Min, Max));
    }
}
