#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalPuppetDrowningPatchTests
{
    // Each drowning happens on the agent's owner, so a client never drowns another owner's puppet (R43 self-hits).
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void DrownsAgent_OnlyAnAgentThisClientControlsOrDoesNotNetwork(bool isRegistered, bool isLocallyControlled, bool expected)
    {
        Assert.Equal(expected, NavalPuppetDrowningPatch.DrownsAgent(isRegistered, isLocallyControlled));
    }
}
#endif
