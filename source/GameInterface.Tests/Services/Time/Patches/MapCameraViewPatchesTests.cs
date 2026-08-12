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
    public void ShouldTickMapCamera_WhenMapMenuIsActive_ReturnsFalse()
    {
        Assert.False(MapCameraViewPatches.ShouldTickMapCamera(
            new MapState(), isInMenu: true));
    }

    [Fact]
    public void ShouldTickMapCamera_WhenMapConversationIsActive_ReturnsFalse()
    {
        Assert.False(MapCameraViewPatches.ShouldTickMapCamera(
            new MapState(), isConversationActive: true));
    }

    [Fact]
    public void ShouldTickMapCamera_WhenEncounterTransitionIsActive_ReturnsFalse()
    {
        Assert.False(MapCameraViewPatches.ShouldTickMapCamera(
            new MapState(), isEncounterActive: true));
    }

    [Fact]
    public void ShouldTickMapCamera_WhenMenuStateIsActive_ReturnsFalse()
    {
        Assert.False(MapCameraViewPatches.ShouldTickMapCamera(new InventoryState()));
    }

    [Fact]
    public void ShouldTickMapCamera_WhenNoStateIsActive_ReturnsFalse()
    {
        Assert.False(MapCameraViewPatches.ShouldTickMapCamera(null));
    }
}
