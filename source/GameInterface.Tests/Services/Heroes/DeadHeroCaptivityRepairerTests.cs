using Common.Util;
using GameInterface.Services.Heroes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class DeadHeroCaptivityRepairerTests
{
    private readonly DeadHeroCaptivityRepairer repairer = new();

    [Fact]
    public void TryRestoreDeadState_CompletedDeathPrisoner_RestoresStateWithoutRehydratingProgression()
    {
        var hero = CreateCompletedDeathHero(Hero.CharacterStates.Prisoner);
        var deathDay = hero._deathDay;
        var deathMark = hero.DeathMark;

        Assert.True(repairer.TryRestoreDeadState(hero));

        Assert.Equal(Hero.CharacterStates.Dead, hero.HeroState);
        Assert.Equal(deathDay, hero._deathDay);
        Assert.Equal(deathMark, hero.DeathMark);
        Assert.Null(hero._heroSkills);
        Assert.Null(hero._heroPerks);
        Assert.Null(hero._heroTraits);
        Assert.Null(hero._characterAttributes);
        Assert.Null(hero._heroDeveloper);
        Assert.Null(hero.VolunteerTypes);
    }

    [Fact]
    public void TryRestoreDeadState_NormalPrisoner_DoesNotChangeState()
    {
        var hero = new Hero();
        hero._heroState = Hero.CharacterStates.Prisoner;

        Assert.False(repairer.TryRestoreDeadState(hero));
        Assert.Equal(Hero.CharacterStates.Prisoner, hero.HeroState);
    }

    [Fact]
    public void TryRestoreDeadState_PartialOrDeferredDeath_DoesNotChangeState()
    {
        var partiallyCleared = CreateCompletedDeathHero(Hero.CharacterStates.Prisoner);
        partiallyCleared._heroPerks = new Hero()._heroPerks;

        var deferredDeath = new Hero();
        deferredDeath._heroState = Hero.CharacterStates.Active;
        deferredDeath.DeathMark = KillCharacterAction.KillCharacterActionDetail.DiedInBattle;

        var missingDeathDay = CreateCompletedDeathHero(Hero.CharacterStates.Prisoner);
        missingDeathDay._deathDay = CampaignTime.Never;

        var missingDeathMark = CreateCompletedDeathHero(Hero.CharacterStates.Prisoner);
        missingDeathMark.DeathMark = KillCharacterAction.KillCharacterActionDetail.None;

        foreach (var hero in new[] { partiallyCleared, deferredDeath, missingDeathDay, missingDeathMark })
        {
            var originalState = hero.HeroState;

            Assert.False(repairer.TryRestoreDeadState(hero));
            Assert.Equal(originalState, hero.HeroState);
        }
    }

    [Fact]
    public void RepairCaptivityRosters_CompletedDeathHero_RemovesOnlyTargetAndIsIdempotent()
    {
        var hero = CreateCompletedDeathHero(Hero.CharacterStates.Prisoner);
        var heroCharacter = new CharacterObject();
        heroCharacter.HeroObject = hero;
        hero._characterObject = heroCharacter;

        var unrelatedPrisoner = new CharacterObject();
        var prisonRoster = new TroopRoster();
        prisonRoster.data = new[]
        {
            new TroopRosterElement(heroCharacter) { Number = 1, WoundedNumber = 1 },
            new TroopRosterElement(unrelatedPrisoner) { Number = 3, WoundedNumber = 1 },
        };
        prisonRoster._count = 2;

        var captor = ObjectHelper.SkipConstructor<PartyBase>();
        captor.PrisonRoster = prisonRoster;
        hero.PartyBelongedToAsPrisoner = captor;
        var objectManager = new CampaignObjectManager();
        objectManager._aliveHeroes.Add(hero);

        Assert.True(repairer.TryRestoreDeadState(hero));
        Assert.Equal(1, repairer.RepairLoadedState(objectManager));

        Assert.Null(hero.PartyBelongedToAsPrisoner);
        Assert.DoesNotContain(hero, objectManager.AliveHeroes);
        Assert.Contains(hero, objectManager.DeadOrDisabledHeroes);
        Assert.Equal(-1, prisonRoster.FindIndexOfTroop(heroCharacter));
        Assert.Equal(3, prisonRoster.GetTroopCount(unrelatedPrisoner));
        int unrelatedIndex = prisonRoster.FindIndexOfTroop(unrelatedPrisoner);
        Assert.Equal(1, prisonRoster.GetElementWoundedNumber(unrelatedIndex));
        Assert.Equal(3, prisonRoster.TotalManCount);

        Assert.Equal(0, repairer.RepairLoadedState(objectManager));
        Assert.Equal(3, prisonRoster.GetTroopCount(unrelatedPrisoner));
        Assert.Equal(3, prisonRoster.TotalManCount);
    }

    private static Hero CreateCompletedDeathHero(Hero.CharacterStates state)
    {
        var hero = new Hero();
        hero._heroState = state;
        hero.DeathMark = KillCharacterAction.KillCharacterActionDetail.Lost;
        hero._deathDay = CampaignTime.Hours(1f);
        hero._heroSkills = null;
        hero._heroPerks = null;
        hero._heroTraits = null;
        hero._characterAttributes = null;
        hero._heroDeveloper = null;
        hero.VolunteerTypes = null;
        return hero;
    }
}
