#if DEBUG
using System;
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalRopeDebugCommandsTests
{
    [Theory]
    [InlineData("0", 0, null)]
    [InlineData("3 12", 3, 12)]
    [InlineData("11 0", 11, 0)]
    public void TryParseThrow_AcceptsASourceAndAnOptionalTarget(string argLine, int source, int? target)
    {
        Assert.True(NavalRopeDebugCommands.TryParseThrow(Args(argLine), out int parsedSource, out int? parsedTarget, out var error), error);

        Assert.Equal(source, parsedSource);
        Assert.Equal(target, parsedTarget);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 2 3")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("bow")]
    [InlineData("1 -2")]
    [InlineData("1 stern")]
    [InlineData("+1")]
    public void TryParseThrow_RejectsMalformedIndices(string argLine)
    {
        Assert.False(NavalRopeDebugCommands.TryParseThrow(Args(argLine), out _, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(39.9f, true)]
    [InlineData(40f, true)]
    [InlineData(40.01f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsInRange_MatchesVanillasFortyMetreHookLimit(float distance, bool expected)
    {
        Assert.Equal(expected, NavalRopeDebugCommands.IsInRange(distance));
    }

    private static string[] Args(string line) => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
}
#endif
