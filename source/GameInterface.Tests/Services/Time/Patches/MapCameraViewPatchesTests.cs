using GameInterface.Services.Time.Patches;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.Time.Patches;

/// <summary>
/// Tests the background map camera gate used while other game states are active.
/// </summary>
public class MapCameraViewPatchesTests
{
    [Fact]
    public void ShouldTickMapCamera_WhenMissionIsActive_ReturnsFalse()
    {
        Assert.False(MapCameraViewPatches.ShouldTickMapCamera(new MissionState()));
    }

    [Fact]
    public void ShouldTickMapCamera_WhenMapIsActive_ReturnsTrue()
    {
        Assert.True(MapCameraViewPatches.ShouldTickMapCamera(new MapState()));
    }

    [Fact]
    public void ShouldTickMapCamera_WhenMenuStateIsActive_ReturnsTrue()
    {
        Assert.True(MapCameraViewPatches.ShouldTickMapCamera(new InventoryState()));
    }
}
