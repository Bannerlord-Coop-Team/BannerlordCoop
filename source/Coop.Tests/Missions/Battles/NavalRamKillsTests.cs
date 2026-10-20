#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalRamKillsTests
{
    // Each ram death happens on the agent's owner, so a client never kills another owner's puppet at the impact.
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void KillsAgent_OnlyAnAgentThisClientControlsOrDoesNotNetwork(bool isRegistered, bool isLocallyControlled, bool expected)
    {
        Assert.Equal(expected, NavalRamKills.KillsAgent(isRegistered, isLocallyControlled));
    }

    [Fact]
    public void ExitRamTick_EndsTheSparing()
    {
        NavalRamKills.EnterRamTick();
        NavalRamKills.ExitRamTick();

        Assert.False(NavalRamKills.IsInRamTick);
    }
}
#endif
