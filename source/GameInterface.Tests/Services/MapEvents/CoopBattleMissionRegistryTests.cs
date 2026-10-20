using GameInterface.Services.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests the loaded-mission window of <see cref="CoopBattleMissionRegistry"/>.</summary>
public class CoopBattleMissionRegistryTests
{
    // A fresh registry reports no coop battle mission, matching the vanilla default.
    [Fact]
    public void IsMissionActive_FalseBeforeAnyMissionBegins()
    {
        var registry = new CoopBattleMissionRegistry();

        Assert.False(registry.IsMissionActive);
    }

    // Beginning and ending the same mission opens and closes the window.
    [Fact]
    public void EndMission_SameId_ClosesWindow()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginMission("MapEvent_1");
        Assert.True(registry.IsMissionActive);

        registry.EndMission("MapEvent_1");
        Assert.False(registry.IsMissionActive);
    }

    // A stale controller disposed after a newer mission began must not close the newer window.
    [Fact]
    public void EndMission_StaleId_KeepsNewerWindowOpen()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginMission("MapEvent_1");
        registry.BeginMission("MapEvent_2");
        registry.EndMission("MapEvent_1");

        Assert.True(registry.IsMissionActive);

        registry.EndMission("MapEvent_2");
        Assert.False(registry.IsMissionActive);
    }

    // A mission that loaded before its instance id was known still opens and closes its window.
    [Fact]
    public void EndMission_NullId_ClosesWindowBegunWithNullId()
    {
        var registry = new CoopBattleMissionRegistry();

        registry.BeginMission(null);
        Assert.True(registry.IsMissionActive);

        registry.EndMission(null);
        Assert.False(registry.IsMissionActive);
    }
}
