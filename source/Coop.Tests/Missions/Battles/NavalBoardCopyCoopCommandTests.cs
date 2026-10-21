#if DEBUG
using Missions.Naval;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalBoardCopyCoopCommandTests
{
    private const float Tolerance = 0.001f;

    // A hull turned a quarter turn and moved: the error is measured on the hull's deck plane, not the world's.
    private static readonly MatrixFrame Hull = new MatrixFrame(
        new Mat3(new Vec3(0f, 1f, 0f), new Vec3(-1f, 0f, 0f), new Vec3(0f, 0f, 1f)), new Vec3(100f, 50f, 2f));

    [Fact]
    public void DeckError_IsZeroWhenTheAgentStandsOnTheSpot()
    {
        var spot = Hull.TransformToParent(new Vec3(1f, 2f, 3f));

        Assert.Equal(0f, NavalBoardCopyCoopCommand.DeckError(Hull, spot, spot), 3);
    }

    [Fact]
    public void DeckError_IsTheHorizontalDistanceInTheHullFrame()
    {
        var spot = Hull.TransformToParent(new Vec3(1f, 2f, 3f));
        var agent = Hull.TransformToParent(new Vec3(4f, 6f, 10f));

        Assert.InRange(NavalBoardCopyCoopCommand.DeckError(Hull, spot, agent), 5f - Tolerance, 5f + Tolerance);
    }

    [Fact]
    public void DeckError_IgnoresHeightAboveTheSpot()
    {
        var spot = Hull.TransformToParent(new Vec3(1f, 2f, 3f));
        var agent = Hull.TransformToParent(new Vec3(1f, 2f, 4.5f));

        Assert.InRange(NavalBoardCopyCoopCommand.DeckError(Hull, spot, agent), 0f, Tolerance);
    }
}
#endif
