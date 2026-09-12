using GameInterface.Services.Party.Handlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PartyDoneLogicHeroCaptureTests
{
    [Fact]
    public void FilterIneligibleTakenHeroes_RemovesDeathMarkedHeroAndPreservesEligibleEntries()
    {
        var deathMarkedHero = CreateHero(Hero.CharacterStates.Active, KillCharacterAction.KillCharacterActionDetail.Lost);
        var eligibleHero = CreateHero(Hero.CharacterStates.Active, KillCharacterAction.KillCharacterActionDetail.None);
        var ordinaryTroop = new CharacterObject();
        var takenPrisoners = new FlattenedTroopRoster(4);
        AddRosterElement(takenPrisoners, deathMarkedHero.CharacterObject, 1);
        AddRosterElement(takenPrisoners, eligibleHero.CharacterObject, 2);
        AddRosterElement(takenPrisoners, ordinaryTroop, 3);

        var filtered = PartyDoneLogicHandler.FilterIneligibleTakenHeroes(takenPrisoners);

        Assert.DoesNotContain(filtered, element => element.Troop == deathMarkedHero.CharacterObject);
        Assert.Contains(filtered, element => element.Troop == eligibleHero.CharacterObject);
        Assert.Contains(filtered, element => element.Troop == ordinaryTroop);
    }

    private static void AddRosterElement(
        FlattenedTroopRoster roster,
        CharacterObject character,
        int uniqueSeed)
    {
        var descriptor = new UniqueTroopDescriptor(uniqueSeed);
        roster[descriptor] = new FlattenedTroopRosterElement(character, 0, 0, descriptor, 0);
    }

    private static Hero CreateHero(
        Hero.CharacterStates state,
        KillCharacterAction.KillCharacterActionDetail deathMark)
    {
        var hero = new Hero();
        var character = new CharacterObject();
        character.HeroObject = hero;
        hero._characterObject = character;
        hero._heroState = state;
        hero.DeathMark = deathMark;
        return hero;
    }
}
