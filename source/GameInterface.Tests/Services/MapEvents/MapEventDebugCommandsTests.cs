#if DEBUG
using GameInterface.Services.Villages.Commands;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventDebugCommandsTests
{
    [Theory]
    [InlineData("450", "1", 450, 1)]
    [InlineData("900", "2", 900, 2)]
    public void TryParseLateEnemyTargets_ValidTargets_ReturnsValues(
        string healthyTarget,
        string lordTarget,
        int expectedHealthy,
        int expectedLords)
    {
        bool parsed = MapEventDebugCommands.TryParseLateEnemyTargets(
            new List<string> { healthyTarget, lordTarget },
            out int healthy,
            out int lords);

        Assert.True(parsed);
        Assert.Equal(expectedHealthy, healthy);
        Assert.Equal(expectedLords, lords);
    }

    [Theory]
    [InlineData("4", "1")]
    [InlineData("901", "1")]
    [InlineData("450", "0")]
    [InlineData("450", "3")]
    public void TryParseLateEnemyTargets_InvalidTargets_ReturnsFalse(
        string healthyTarget,
        string lordTarget)
    {
        bool parsed = MapEventDebugCommands.TryParseLateEnemyTargets(
            new List<string> { healthyTarget, lordTarget },
            out _,
            out _);

        Assert.False(parsed);
    }
}
#endif
