#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalAgentTickGateTests
{
    // Outside a mission there is no async agent tick to wait for.
    [Fact]
    public void WaitForAgentTick_WithoutAMission_ReturnsAtOnce()
    {
        Assert.True(new NavalAgentTickGate().WaitForAgentTick("a test"));
    }
}
#endif
