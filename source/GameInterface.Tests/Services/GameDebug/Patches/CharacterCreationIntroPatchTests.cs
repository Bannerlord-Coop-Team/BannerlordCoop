using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Patches;
using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
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
    [InlineData(true, typeof(TaleworldGameState), true)]
    [InlineData(false, typeof(CharacterCreationState), true)]
    [InlineData(false, typeof(MapState), false)]
    [InlineData(false, typeof(TaleworldGameState), false)]
    public void ShouldAutomate_RunsForTheIntroOrADirectCharacterCreationStart(bool inIntro, Type stateType, bool expected)
    {
        // A mod can skip the campaign intro video and open character creation directly.
        Assert.Equal(expected, CharacterCreationIntroPatch.ShouldAutomate(inIntro, stateType));
    }

    [Theory]
    [InlineData(new[] { "Battania", "Empire", "Vlandia" }, 1)]
    [InlineData(new[] { "Dunland", "Gondor" }, 0)]
    public void ChooseCultureIndex_PrefersEmpireElseTheFirstCulture(string[] names, int expected)
    {
        Assert.Equal(expected, DebugCharacterCreationInterface.ChooseCultureIndex(names));
    }

    [Theory]
    [InlineData(true, false, false, (int)DebugCharacterCreationInterface.SkipEntry.Inline)]
    [InlineData(false, true, false, (int)DebugCharacterCreationInterface.SkipEntry.NextFrame)]
    [InlineData(false, true, true, (int)DebugCharacterCreationInterface.SkipEntry.None)]
    [InlineData(false, false, false, (int)DebugCharacterCreationInterface.SkipEntry.None)]
    public void ChooseSkipEntry_WaitsAFrameWhenCharacterCreationOpensDirectly(
        bool inIntro, bool inCharacterCreation, bool skipping, int expected)
    {
        // A direct start activates the state before its screen and stage views exist.
        Assert.Equal((DebugCharacterCreationInterface.SkipEntry)expected, DebugCharacterCreationInterface.ChooseSkipEntry(inIntro, inCharacterCreation, skipping));
    }
}
