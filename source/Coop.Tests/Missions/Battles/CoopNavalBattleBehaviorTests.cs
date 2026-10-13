#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopNavalBattleBehaviorTests
{
    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(120, 111, false)]
    [InlineData(120, 110, true)]
    [InlineData(120, 0, true)]
    [InlineData(0, 120, false)]
    public void IsAgentCountDrop_FlagsOnlyALargeDropAfterTheFirstCount(int before, int after, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleBehavior.IsAgentCountDrop(before, after));
    }
}
#endif
