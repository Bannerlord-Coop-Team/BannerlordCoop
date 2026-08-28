using GameInterface.Services.GameDebug.Patches;
using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.MountAndBlade;
using Xunit;
using TaleworldGameState = TaleWorlds.Core.GameState;

namespace GameInterface.Tests.Services.GameDebug.Patches;

public class CharacterCreationIntroPatchTests
{
    [Fact]
    public void IsCharacterCreationState_ReturnsTrueForCharacterCreation()
    {
        Assert.True(CharacterCreationIntroPatch.IsCharacterCreationState(typeof(CharacterCreationState)));
    }

    [Theory]
    [InlineData(typeof(TaleworldGameState))]
    [InlineData(typeof(MapState))]
    public void IsCharacterCreationState_ReturnsFalseForOtherGameStates(Type stateType)
    {
        Assert.False(CharacterCreationIntroPatch.IsCharacterCreationState(stateType));
    }

    [Theory]
    [InlineData(@"C:\Bannerlord\Modules\SandBox\Videos\CampaignIntro\campaign_intro.ivf")]
    [InlineData("/bannerlord/Modules/SandBox/Videos/CampaignIntro/CAMPAIGN_INTRO.IVF")]
    [InlineData("campaign_intro.ivf")]
    public void IsCharacterCreationIntro_ReturnsTrueForCampaignIntroVideo(string videoPath)
    {
        var state = new VideoPlaybackState();
        state.SetStartingParameters(
            videoPath,
            @"C:\Bannerlord\Modules\SandBox\Videos\CampaignIntro\campaign_intro.ogg",
            "campaign_intro");

        Assert.True(CharacterCreationIntroPatch.IsCharacterCreationIntro(state));
    }

    [Theory]
    [InlineData("launcher_logo.ivf")]
    [InlineData("campaign_intro_teaser.ivf")]
    [InlineData("intro_campaign_intro_backup.ivf")]
    public void IsCharacterCreationIntro_ReturnsFalseForUnrelatedVideo(string videoPath)
    {
        var state = new VideoPlaybackState();
        state.SetStartingParameters(videoPath, "audio.ogg", "subtitles");

        Assert.False(CharacterCreationIntroPatch.IsCharacterCreationIntro(state));
    }

    [Fact]
    public void IsCharacterCreationIntro_ReturnsFalseWithoutVideoPath()
    {
        Assert.False(CharacterCreationIntroPatch.IsCharacterCreationIntro(new VideoPlaybackState()));
    }

    [Fact]
    public void IsCharacterCreationIntro_ReturnsFalseForNonVideoState()
    {
        Assert.False(CharacterCreationIntroPatch.IsCharacterCreationIntro(new MapState()));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void ShouldHideCoopLoadingWindow_RequiresForcedClientIntro(
        bool isCharacterCreationIntro,
        bool forceLoadingWindow,
        bool isClient,
        bool expected)
    {
        Assert.Equal(
            expected,
            CharacterCreationIntroPatch.ShouldHideCoopLoadingWindow(
                isCharacterCreationIntro,
                forceLoadingWindow,
                isClient));
    }
}
