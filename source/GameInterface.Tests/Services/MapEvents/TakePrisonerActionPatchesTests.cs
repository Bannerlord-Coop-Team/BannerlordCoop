using GameInterface.Services.MapEvents.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class TakePrisonerActionPatchesTests
{
    [Theory]
    [InlineData(Hero.CharacterStates.Active, KillCharacterAction.KillCharacterActionDetail.None, true)]
    [InlineData(Hero.CharacterStates.Dead, KillCharacterAction.KillCharacterActionDetail.None, false)]
    [InlineData(Hero.CharacterStates.Dead, KillCharacterAction.KillCharacterActionDetail.Lost, false)]
    [InlineData(Hero.CharacterStates.Active, KillCharacterAction.KillCharacterActionDetail.Lost, false)]
    [InlineData(Hero.CharacterStates.Active, KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false)]
    [InlineData(Hero.CharacterStates.Active, KillCharacterAction.KillCharacterActionDetail.DiedInLabor, false)]
    public void CanCaptureHero_RequiresAliveHeroWithoutDeathMark(
        Hero.CharacterStates heroState,
        KillCharacterAction.KillCharacterActionDetail deathMark,
        bool expected)
    {
        var hero = new Hero();
        hero._heroState = heroState;
        hero.DeathMark = deathMark;

        Assert.Equal(expected, TakePrisonerActionPatches.CanCaptureHero(hero));
    }
}
