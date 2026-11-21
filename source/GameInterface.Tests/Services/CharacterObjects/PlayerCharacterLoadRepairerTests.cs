using Common.Util;
using GameInterface.Services.CharacterObjects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.CharacterObjects;

/// <summary>
/// Joined players' characters get their template link and hero culture back around the after-load refill (#4675).
/// </summary>
public class PlayerCharacterLoadRepairerTests
{
    private readonly PlayerCharacterLoadRepairer repairer = new();

    [Theory]
    [InlineData("Player", true)]
    [InlineData("Player10676", true)]
    [InlineData("Player_1", false)]
    [InlineData("PlayerOne", false)]
    [InlineData("player1", false)]
    [InlineData("lord_Player1", false)]
    [InlineData("main_hero", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsJoinedPlayerId_MatchesOnlyTheIdsTheHostGivesJoinedHeroes(string stringId, bool expected)
    {
        Assert.Equal(expected, PlayerCharacterLoadRepairer.IsJoinedPlayerId(stringId));
    }

    [Fact]
    public void RepairLoadedPlayers_JoinedHeroFromAnOldSave_LinksItToTheTemplate()
    {
        var mainHero = Character("main_hero");
        var joined = HeroWith(Character("Player10676"));

        var repaired = repairer.RepairLoadedPlayers(new[] { joined }, mainHero);

        Assert.Equal(1, repaired);
        Assert.Same(mainHero, joined.CharacterObject.OriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_HollowLordWithAnotherId_IsLeftForItsOwnRepair()
    {
        var lord = HeroWith(Character("lord_1_1"));

        var repaired = repairer.RepairLoadedPlayers(new[] { lord }, Character("main_hero"));

        Assert.Equal(0, repaired);
        Assert.True(lord.CharacterObject.IsOriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_JoinedCharacterThatIsReady_IsLeftAlone()
    {
        var character = Character("Player");
        character.IsReady = true;
        var joined = HeroWith(character);

        var repaired = repairer.RepairLoadedPlayers(new[] { joined }, Character("main_hero"));

        Assert.Equal(0, repaired);
        Assert.True(character.IsOriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_JoinedHeroThatHasALink_KeepsIt()
    {
        var ownTemplate = Character("main_hero");
        var character = Character("Player");
        character._originCharacter = ownTemplate;

        var repaired = repairer.RepairLoadedPlayers(new[] { HeroWith(character) }, Character("other"));

        Assert.Equal(0, repaired);
        Assert.Same(ownTemplate, character.OriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_ChildMadeFromAHollowJoinedHero_GetsTheTemplateAsItsOrigin()
    {
        var mainHero = Character("main_hero");
        var parent = HeroWith(Character("Player"));
        var childCharacter = Character("CharacterObject_9");
        // CreateFrom made the unlinked parent the child's origin
        childCharacter._originCharacter = parent.CharacterObject;
        var child = HeroWith(childCharacter);

        repairer.RepairLoadedPlayers(new[] { child, parent }, mainHero);

        // What CreateFrom gives a child of a linked parent: origin = parent's origin
        Assert.Same(mainHero, childCharacter.OriginalCharacter);
    }

    [Fact]
    public void RestoreHeroCultures_AfterTheRefill_GivesTheCharacterItsHerosCulture()
    {
        var mainHero = ObjectHelper.SkipConstructor<CharacterObject>();
        var templateCulture = ObjectHelper.SkipConstructor<CultureObject>();
        mainHero._culture = templateCulture;
        var joined = HeroWith(Character("Player"));
        var playerCulture = ObjectHelper.SkipConstructor<CultureObject>();
        joined.Culture = playerCulture;

        PlayerCharacterTemplate.Link(joined.CharacterObject, mainHero);
        joined.CharacterObject.InitializeHeroCharacterOnAfterLoad();
        // The refill copied the template's culture into the field an agent's Character reads
        Assert.Same(templateCulture, ((BasicCharacterObject)joined.CharacterObject).Culture);

        var restored = repairer.RestoreHeroCultures(new[] { joined }, mainHero);

        Assert.Equal(1, restored);
        Assert.Same(playerCulture, ((BasicCharacterObject)joined.CharacterObject).Culture);
    }

    [Fact]
    public void RestoreHeroCultures_CharacterWithAnotherTemplate_KeepsItsCulture()
    {
        var wandererTemplate = Character("spc_wanderer_1");
        var templateCulture = ObjectHelper.SkipConstructor<CultureObject>();
        var wanderer = HeroWith(Character("CharacterObject_5"));
        wanderer.CharacterObject._originCharacter = wandererTemplate;
        wanderer.CharacterObject._culture = templateCulture;
        wanderer.Culture = ObjectHelper.SkipConstructor<CultureObject>();

        var restored = repairer.RestoreHeroCultures(new[] { wanderer }, Character("main_hero"));

        Assert.Equal(0, restored);
        Assert.Same(templateCulture, ((BasicCharacterObject)wanderer.CharacterObject).Culture);
    }

    [Fact]
    public void RestoreHeroCultures_TheTemplatesOwnHero_KeepsItsCulture()
    {
        var mainHeroCharacter = Character("main_hero");
        var xmlCulture = ObjectHelper.SkipConstructor<CultureObject>();
        mainHeroCharacter._culture = xmlCulture;
        var host = HeroWith(mainHeroCharacter);
        host.Culture = ObjectHelper.SkipConstructor<CultureObject>();

        var restored = repairer.RestoreHeroCultures(new[] { host }, mainHeroCharacter);

        Assert.Equal(0, restored);
        Assert.Same(xmlCulture, ((BasicCharacterObject)mainHeroCharacter).Culture);
    }

    private static CharacterObject Character(string stringId)
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        character.StringId = stringId;
        return character;
    }

    private static Hero HeroWith(CharacterObject character)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._characterObject = character;
        character._heroObject = hero;
        return hero;
    }
}
