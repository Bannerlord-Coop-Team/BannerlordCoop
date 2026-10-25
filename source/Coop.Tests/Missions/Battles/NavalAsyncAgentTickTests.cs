#if COOP_NAVAL
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R48 re-review: work outside the mission tick reads whether the async agent tick runs, without waiting.</summary>
[Collection("Mission.Current")]
public class NavalAsyncAgentTickTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void IsRunning_OnlyOutsideTheMissionTickWithTheTickPending(bool inMissionTick, bool tickCompleted, bool expected)
    {
        Assert.Equal(expected, NavalAsyncAgentTick.IsTickRunning(inMissionTick, tickCompleted));
    }

    // Without a mission there is no async agent tick.
    [Fact]
    public void IsRunning_WithoutAMission_IsFalse()
    {
        Assert.False(new NavalAsyncAgentTick().IsRunning);
    }
}
#endif
