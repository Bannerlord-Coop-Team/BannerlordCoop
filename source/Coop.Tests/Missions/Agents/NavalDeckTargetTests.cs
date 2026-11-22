using Missions.Agents;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class NavalDeckTargetTests
{
    private const float Tolerance = 0.001f;

    // A hull turned a quarter turn and moved: the error is measured on the hull's deck plane, not the world's.
    private static readonly MatrixFrame Hull = new MatrixFrame(
        new Mat3(new Vec3(0f, 1f, 0f), new Vec3(-1f, 0f, 0f), new Vec3(0f, 0f, 1f)), new Vec3(100f, 50f, 2f));

    private static readonly Vec3 Spot = new Vec3(1f, 2f, 3f);

    [Fact]
    public void HorizontalError_IsZeroWhenTheAgentStandsOnTheSpot()
    {
        Assert.Equal(0f, NavalDeckTarget.HorizontalError(Hull, Spot, Hull.TransformToParent(Spot)), 3);
    }

    [Fact]
    public void HorizontalError_IsTheHorizontalDistanceInTheHullFrame()
    {
        var agent = Hull.TransformToParent(new Vec3(4f, 6f, 10f));

        Assert.InRange(NavalDeckTarget.HorizontalError(Hull, Spot, agent), 5f - Tolerance, 5f + Tolerance);
    }

    [Fact]
    public void HorizontalError_IgnoresHeightAboveTheSpot()
    {
        var agent = Hull.TransformToParent(new Vec3(1f, 2f, 4.5f));

        Assert.InRange(NavalDeckTarget.HorizontalError(Hull, Spot, agent), 0f, Tolerance);
    }
}
