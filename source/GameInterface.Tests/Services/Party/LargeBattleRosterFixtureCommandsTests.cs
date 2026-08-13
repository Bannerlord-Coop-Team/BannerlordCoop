#if DEBUG
using GameInterface.Services.Party.Commands;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class LargeBattleRosterFixtureCommandsTests
{
    [Fact]
    public void TryParseExactHealthyTargets_OneTarget_UsesSameCountForBothParties()
    {
        var args = new List<string> { "first", "second", "900" };

        bool parsed = LargeBattleRosterFixtureCommands.TryParseExactHealthyTargets(
            args,
            out int first,
            out int second);

        Assert.True(parsed);
        Assert.Equal(900, first);
        Assert.Equal(900, second);
    }

    [Fact]
    public void TryParseExactHealthyTargets_TwoTargets_PreservesAsymmetricCounts()
    {
        var args = new List<string> { "first", "second", "600", "300" };

        bool parsed = LargeBattleRosterFixtureCommands.TryParseExactHealthyTargets(
            args,
            out int first,
            out int second);

        Assert.True(parsed);
        Assert.Equal(600, first);
        Assert.Equal(300, second);
    }

    [Theory]
    [InlineData("4", "300")]
    [InlineData("600", "901")]
    [InlineData("not-a-count", "300")]
    public void TryParseExactHealthyTargets_InvalidCount_ReturnsFalse(
        string firstTarget,
        string secondTarget)
    {
        var args = new List<string> { "first", "second", firstTarget, secondTarget };

        bool parsed = LargeBattleRosterFixtureCommands.TryParseExactHealthyTargets(
            args,
            out _,
            out _);

        Assert.False(parsed);
    }
}
#endif
