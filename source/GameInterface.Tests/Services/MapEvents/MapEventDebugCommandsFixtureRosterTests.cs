using GameInterface.Services.Villages.Commands;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

#if DEBUG
public class MapEventDebugCommandsFixtureRosterTests
{
    [Fact]
    public void LimitLateJoinModeFixtureRoster_RemovesRegularTroopsAfterWoundedEntries()
    {
        var roster = new TroopRoster();
        var healthyTroop = new CharacterObject();
        var woundedTroop = new CharacterObject();
        roster.AddToCounts(healthyTroop, 8);
        roster.AddToCounts(woundedTroop, 6, woundedCount: 6);

        MapEventDebugCommands.LimitLateJoinModeFixtureRoster(roster);

        Assert.Equal(0, roster.GetTroopCount(healthyTroop));
        Assert.Equal(0, roster.GetTroopCount(woundedTroop));
        Assert.Equal(0, roster.TotalHealthyCount);
    }

    [Fact]
    public void LimitLateJoinModeFixtureRoster_PreservesHeroesWhileLimitingRegularTroops()
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        var leader = CreateHeroCharacter();
        var companion = CreateHeroCharacter();
        var regularTroop = new CharacterObject();
        Assert.True(leader.IsHero);
        Assert.True(companion.IsHero);
        roster.data[0] = new TroopRosterElement(leader) { Number = 1 };
        roster.data[1] = new TroopRosterElement(companion) { Number = 1 };
        roster.data[2] = new TroopRosterElement(regularTroop) { Number = 8 };
        roster._count = 3;

        MapEventDebugCommands.LimitLateJoinModeFixtureRoster(roster);

        Assert.Equal(1, roster.GetTroopCount(leader));
        Assert.Equal(1, roster.GetTroopCount(companion));
        Assert.Equal(0, roster.GetTroopCount(regularTroop));
    }

    [Fact]
    public void LimitLateJoinModeFixtureRoster_UsesRequestedRegularTroopLimit()
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        var leader = CreateHeroCharacter();
        var companion = CreateHeroCharacter();
        var regularTroop = new CharacterObject();
        roster.data[0] = new TroopRosterElement(leader) { Number = 1 };
        roster.data[1] = new TroopRosterElement(companion) { Number = 1 };
        roster.data[2] = new TroopRosterElement(regularTroop) { Number = 8 };
        roster._count = 3;

        MapEventDebugCommands.LimitLateJoinModeFixtureRoster(roster, 1);

        Assert.Equal(1, roster.GetTroopCount(leader));
        Assert.Equal(1, roster.GetTroopCount(companion));
        Assert.Equal(1, roster.GetTroopCount(regularTroop));
    }

    [Fact]
    public void LimitLateJoinModeFixtureRosters_UsesOneRegularTroopAcrossAllRosters()
    {
        var firstRoster = TroopRoster.CreateDummyTroopRoster();
        var secondRoster = TroopRoster.CreateDummyTroopRoster();
        var firstHero = CreateHeroCharacter();
        var secondHero = CreateHeroCharacter();
        var firstRegular = new CharacterObject();
        var secondRegular = new CharacterObject();
        firstRoster.data[0] = new TroopRosterElement(firstHero) { Number = 1 };
        firstRoster.data[1] = new TroopRosterElement(firstRegular) { Number = 8 };
        firstRoster._count = 2;
        secondRoster.data[0] = new TroopRosterElement(secondHero) { Number = 1 };
        secondRoster.data[1] = new TroopRosterElement(secondRegular) { Number = 8 };
        secondRoster._count = 2;

        MapEventDebugCommands.LimitLateJoinModeFixtureRosters(
            new[] { firstRoster, secondRoster },
            1);

        Assert.Equal(1, firstRoster.GetTroopCount(firstHero));
        Assert.Equal(1, secondRoster.GetTroopCount(secondHero));
        Assert.Equal(1, firstRoster.GetTroopCount(firstRegular));
        Assert.Equal(0, secondRoster.GetTroopCount(secondRegular));
    }

    [Fact]
    public void RestoreLateJoinModeFixtureMemberRoster_RestoresCountsWoundsAndXp()
    {
        var roster = new TroopRoster();
        var firstTroop = new CharacterObject();
        var secondTroop = new CharacterObject();
        roster.AddToCounts(firstTroop, 8, woundedCount: 2, xpChange: 73);
        roster.AddToCounts(secondTroop, 5, woundedCount: 1, xpChange: 41);
        var snapshot = roster.GetTroopRoster().ToArray();

        MapEventDebugCommands.LimitLateJoinModeFixtureRoster(roster);
        MapEventDebugCommands.RestoreLateJoinModeFixtureMemberRoster(roster, snapshot);

        AssertRosterElement(roster, firstTroop, 8, 2, 73);
        AssertRosterElement(roster, secondTroop, 5, 1, 41);
    }

    private static void AssertRosterElement(
        TroopRoster roster,
        CharacterObject character,
        int number,
        int woundedNumber,
        int xp)
    {
        var element = roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(character));
        Assert.Equal(number, element.Number);
        Assert.Equal(woundedNumber, element.WoundedNumber);
        Assert.Equal(xp, element.Xp);
    }

    private static CharacterObject CreateHeroCharacter()
    {
        var hero = new Hero();
        var character = new CharacterObject();
        character.HeroObject = hero;
        hero._characterObject = character;
        return character;
    }
}
#endif
