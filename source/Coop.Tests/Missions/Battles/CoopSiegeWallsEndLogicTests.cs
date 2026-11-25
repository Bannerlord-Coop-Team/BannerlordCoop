using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopSiegeWallsEndLogicTests
{
    // A full rout ends the walls into the keep once vanilla's pull-back count is met and the defenders kept fleeing for
    // vanilla's retreat wait, instead of waiting for every fleeing defender to leave the scene.
    [Theory]
    [InlineData(true, true, 13f, true)]
    [InlineData(true, false, 13f, false)]
    [InlineData(true, true, 12f, false)]
    [InlineData(false, true, 13f, false)]
    public void EndsAsFallBack_OnlyAfterThePullBackCountAndTheRetreatWait(bool canCheckForEnd, bool pulledBack,
        float secondsAllRunningAway, bool expected)
    {
        Assert.Equal(expected, CoopSiegeWallsEndLogic.EndsAsFallBack(canCheckForEnd, pulledBack, secondsAllRunningAway, 12f));
    }
}
