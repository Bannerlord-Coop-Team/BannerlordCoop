#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

// The hold reads Mission.Current, which other test classes in this collection set.
[Collection("Mission.Current")]
public class MissionMenuRefreshHoldPatchTests
{
    // M6 D5: the server's applied ship fates reach clients still in the battle, where the menu context can have no handler.
    [Fact]
    public void MenuRefresh_IsHeldWhileAMissionIsCurrent()
    {
        using (new MissionCurrentScope())
            Assert.False(MissionMenuRefreshHoldPatch.Prefix());

        Assert.True(MissionMenuRefreshHoldPatch.Prefix());
    }
}
#endif
