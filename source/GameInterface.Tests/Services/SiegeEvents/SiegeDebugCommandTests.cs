#if DEBUG
using GameInterface.Services.SiegeEvents.Commands;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeDebugCommandTests
{
    [Fact]
    public void IsSallyOutLeadershipRestored_MatchingLeaders_ReturnsTrue()
    {
        Assert.True(SiegeDebugCommand.IsSallyOutLeadershipRestored("player-party", "player-party"));
    }

    [Theory]
    [InlineData(null, "replacement")]
    [InlineData("camp-leader", null)]
    [InlineData("camp-leader", "replacement")]
    public void IsSallyOutLeadershipRestored_MissingOrDifferentLeader_ReturnsFalse(
        string? campLeader,
        string? defenderLeader)
    {
        Assert.False(SiegeDebugCommand.IsSallyOutLeadershipRestored(campLeader, defenderLeader));
    }
}
#endif
