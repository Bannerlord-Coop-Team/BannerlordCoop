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
}
