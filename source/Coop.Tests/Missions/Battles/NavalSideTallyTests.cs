using Missions.Battles;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalSideTallyTests
{
    private const BattleSideEnum Pirates = BattleSideEnum.Defender;

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 5, false)]
    [InlineData(1, 0, false)]
    public void IsDepleted_NeedsNoLiveTroopAndNoFieldableReserve(int liveHumans, int reserve, bool expected)
    {
        var tally = NavalSideTally.Count(Pirates, liveHumans, new[] { Hull(crew: liveHumans, reserve: reserve) });

        Assert.Equal(expected, tally.IsDepleted);
    }

    [Theory]
    [InlineData(true, 20, 30, true)]
    [InlineData(false, 3, 0, true)]
    [InlineData(false, 1, 2, true)]
    [InlineData(false, 2, 2, false)]
    [InlineData(false, 4, 0, false)]
    [InlineData(false, 0, 0, true)]
    public void IsHullOutOfAction_FollowsVanillasSunkOrThreeTroopsRule(bool isSunk, int crew, int reserve, bool expected)
    {
        Assert.Equal(expected, NavalSideTally.IsHullOutOfAction(isSunk, crew, reserve));
    }

    [Fact]
    public void IsOutOfAction_NeedsHullsAndEveryOneOutOfAction()
    {
        Assert.False(NavalSideTally.Count(Pirates, 0, Enumerable.Empty<NavalHullCount>()).IsOutOfAction);
        Assert.False(NavalSideTally.Count(Pirates, 20, new[] { Hull(sunk: true), Hull(crew: 20) }).IsOutOfAction);
        Assert.True(NavalSideTally.Count(Pirates, 2, new[] { Hull(sunk: true), Hull(crew: 2) }).IsOutOfAction);
    }

    [Fact]
    public void Count_OnlyCountsTheSidesHulls()
    {
        var tally = NavalSideTally.Count(Pirates, 10, new[]
        {
            Hull(crew: 10, reserve: 4),
            new NavalHullCount(BattleSideEnum.Attacker, false, 30, 50),
        });

        Assert.Equal(1, tally.Hulls);
        Assert.Equal(10, tally.CrewAboard);
        Assert.Equal(4, tally.Reserve);
    }

    // R33-R37: a sunk hull is out of action at once, its reserve cannot field, and its swimmers keep the side alive until they drown.
    [Fact]
    public void SunkHull_IsOutOfAction_DropsItsReserveAndCrewAboard_ButItsSwimmersKeepTheSideAlive()
    {
        var tally = NavalSideTally.Count(Pirates, liveHumans: 18, new[] { Hull(sunk: true, crew: 18, reserve: 25) });

        Assert.Equal(1, tally.Hulls);
        Assert.Equal(0, tally.HullsAfloat);
        Assert.Equal(1, tally.HullsOutOfAction);
        Assert.Equal(0, tally.CrewAboard);
        Assert.Equal(0, tally.Reserve);
        Assert.True(tally.IsOutOfAction);
        Assert.False(tally.IsDepleted);
    }

    [Fact]
    public void SunkHull_WhoseSwimmersDrowned_LeavesTheSideDepleted()
    {
        var tally = NavalSideTally.Count(Pirates, liveHumans: 0, new[] { Hull(sunk: true, reserve: 25) });

        Assert.True(tally.IsDepleted);
    }

    [Fact]
    public void PendingReserve_KeepsTheSideAliveWithoutTouchingAnyHull()
    {
        var tally = NavalSideTally.Count(Pirates, 0, new[] { Hull(crew: 0) }, pendingReserve: 12);

        Assert.False(tally.IsDepleted);
        Assert.Equal(12, tally.Reserve);
        Assert.Equal(1, tally.HullsOutOfAction);
    }

    private static NavalHullCount Hull(bool sunk = false, int crew = 0, int reserve = 0) => new(Pirates, sunk, crew, reserve);
}
