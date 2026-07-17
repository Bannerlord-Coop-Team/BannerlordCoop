using GameInterface.Services.Time.Patches;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.Time.Patches;

/// <summary>
/// Tests the background campaign-map tick gate used while another game state is active.
/// </summary>
public class GameStateManagerPatchesTests
{
    [Fact]
    public void ShouldRunBackgroundCampaignSimulation_ForClientFieldBattleMission_ReturnsFalse()
    {
        Assert.False(GameStateManagerPatches.ShouldRunBackgroundCampaignSimulation(
            new MissionState(),
            isClient: true,
            isCoopFieldBattleActive: true));
    }

    [Fact]
    public void ShouldRunBackgroundCampaignSimulation_ForServerFieldBattleMission_ReturnsTrue()
    {
        Assert.True(GameStateManagerPatches.ShouldRunBackgroundCampaignSimulation(
            new MissionState(),
            isClient: false,
            isCoopFieldBattleActive: true));
    }

    [Fact]
    public void ShouldRunBackgroundCampaignSimulation_ForClientOtherMission_ReturnsTrue()
    {
        Assert.True(GameStateManagerPatches.ShouldRunBackgroundCampaignSimulation(
            new MissionState(),
            isClient: true,
            isCoopFieldBattleActive: false));
    }

    [Fact]
    public void ShouldRunBackgroundCampaignSimulation_ForActiveClientMap_ReturnsTrue()
    {
        Assert.True(GameStateManagerPatches.ShouldRunBackgroundCampaignSimulation(
            new MapState(),
            isClient: true,
            isCoopFieldBattleActive: true));
    }
}
