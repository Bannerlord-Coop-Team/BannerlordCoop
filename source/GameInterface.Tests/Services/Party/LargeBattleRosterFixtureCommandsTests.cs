#if DEBUG
using GameInterface.Services.Party.Commands;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class LargeBattleRosterFixtureCommandsTests
{
    [Theory]
    [InlineData(true, false, false, false, 0, 7, true, 7)]
    [InlineData(false, false, false, false, 0, 7, false, 0)]
    [InlineData(true, false, true, false, 0, 7, false, 0)]
    [InlineData(true, true, true, false, 1, 7, false, 0)]
    [InlineData(false, true, true, true, 1, 4, true, 3)]
    [InlineData(true, false, false, false, 0, 4, true, 4)]
    [InlineData(false, true, true, true, 5, 4, false, 0)]
    public void ExactRoster_RequiresHealthyLeaderExceptForHeroLessBandit(
        bool isBandit,
        bool hasLeader,
        bool hasHero,
        bool hasHealthyLeader,
        int healthyHeroes,
        int target,
        bool succeeds,
        int expectedTroops)
    {
        bool result = LargeBattleRosterFixtureCommands.TryGetFixtureTroopCount(
            "party", target, healthyHeroes, hasHealthyLeader, isBandit, hasLeader, hasHero,
            out int troops, out string error);

        Assert.Equal(succeeds, result);
        Assert.Equal(expectedTroops, troops);
        if (succeeds)
            Assert.Null(error);
        else
            Assert.NotNull(error);
    }
}
#endif
