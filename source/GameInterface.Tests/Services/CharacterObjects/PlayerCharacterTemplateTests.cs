using Common.Util;
using GameInterface.Services.CharacterObjects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.CharacterObjects;

/// <summary>
/// A joined player's character gets the player template as its template link (#4675).
/// </summary>
public class PlayerCharacterTemplateTests
{
    [Fact]
    public void Link_CharacterWithoutATemplate_LinksItToTheTemplate()
    {
        var joined = ObjectHelper.SkipConstructor<CharacterObject>();
        var mainHero = ObjectHelper.SkipConstructor<CharacterObject>();

        PlayerCharacterTemplate.Link(joined, mainHero);

        Assert.Same(mainHero, joined.OriginalCharacter);
        Assert.False(joined.IsOriginalCharacter);
    }

    [Fact]
    public void Link_CharacterThatHasATemplate_KeepsItsOwnTemplate()
    {
        var ownTemplate = ObjectHelper.SkipConstructor<CharacterObject>();
        var joined = ObjectHelper.SkipConstructor<CharacterObject>();
        joined._originCharacter = ownTemplate;

        PlayerCharacterTemplate.Link(joined, ObjectHelper.SkipConstructor<CharacterObject>());

        Assert.Same(ownTemplate, joined.OriginalCharacter);
    }

    [Fact]
    public void Link_NoTemplateFound_LeavesTheCharacterUnchanged()
    {
        var joined = ObjectHelper.SkipConstructor<CharacterObject>();

        PlayerCharacterTemplate.Link(joined, null);

        Assert.True(joined.IsOriginalCharacter);
    }

    [Fact]
    public void Link_TheTemplateItself_LeavesItWithoutALink()
    {
        var mainHero = ObjectHelper.SkipConstructor<CharacterObject>();

        PlayerCharacterTemplate.Link(mainHero, mainHero);

        Assert.True(mainHero.IsOriginalCharacter);
    }

    [Fact]
    public void Link_ThenTheEngineAfterLoadRefill_RestoresTheTemplateData()
    {
        var mainHero = ObjectHelper.SkipConstructor<CharacterObject>();
        mainHero._basicName = new TextObject("Player");
        mainHero.DefaultCharacterSkills = ObjectHelper.SkipConstructor<MBCharacterSkills>();
        mainHero.BodyPropertyRange = ObjectHelper.SkipConstructor<MBBodyProperty>();
        mainHero._occupation = Occupation.Lord;

        // After a load a joined character holds only its saved fields, so its template data is unset
        var joined = ObjectHelper.SkipConstructor<CharacterObject>();

        PlayerCharacterTemplate.Link(joined, mainHero);
        // SandBoxManager.InitializeCharactersAfterLoad runs this for every hero character that has a template link
        joined.InitializeHeroCharacterOnAfterLoad();

        Assert.True(joined.IsReady);
        Assert.Same(mainHero._basicName, joined._basicName);
        Assert.Same(mainHero.DefaultCharacterSkills, joined.DefaultCharacterSkills);
        Assert.Same(mainHero.BodyPropertyRange, joined.BodyPropertyRange);
        Assert.Equal(Occupation.Lord, joined._occupation);
    }

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
        Assert.Equal(expected, PlayerCharacterTemplate.IsJoinedPlayerId(stringId));
    }

    [Fact]
    public void RepairLoadedPlayers_JoinedHeroFromAnOldSave_LinksItToTheTemplate()
    {
        var mainHero = Character("main_hero");
        var joined = HeroWith(Character("Player10676"));

        var repaired = PlayerCharacterTemplate.RepairLoadedPlayers(new[] { joined }, mainHero);

        Assert.Equal(1, repaired);
        Assert.Same(mainHero, joined.CharacterObject.OriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_HollowLordWithAnotherId_IsLeftForItsOwnRepair()
    {
        var lord = HeroWith(Character("lord_1_1"));

        var repaired = PlayerCharacterTemplate.RepairLoadedPlayers(new[] { lord }, Character("main_hero"));

        Assert.Equal(0, repaired);
        Assert.True(lord.CharacterObject.IsOriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_JoinedCharacterThatIsReady_IsLeftAlone()
    {
        var character = Character("Player");
        character.IsReady = true;
        var joined = HeroWith(character);

        var repaired = PlayerCharacterTemplate.RepairLoadedPlayers(new[] { joined }, Character("main_hero"));

        Assert.Equal(0, repaired);
        Assert.True(character.IsOriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_JoinedHeroThatHasALink_KeepsIt()
    {
        var ownTemplate = Character("main_hero");
        var character = Character("Player");
        character._originCharacter = ownTemplate;

        var repaired = PlayerCharacterTemplate.RepairLoadedPlayers(new[] { HeroWith(character) }, Character("other"));

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

        PlayerCharacterTemplate.RepairLoadedPlayers(new[] { child, parent }, mainHero);

        // What CreateFrom gives a child of a linked parent: origin = parent's origin
        Assert.Same(mainHero, childCharacter.OriginalCharacter);
    }

    [Fact]
    public void RepairLoadedPlayers_NoTemplateFound_ChangesNothing()
    {
        var joined = HeroWith(Character("Player"));

        var repaired = PlayerCharacterTemplate.RepairLoadedPlayers(new[] { joined }, null);

        Assert.Equal(0, repaired);
        Assert.True(joined.CharacterObject.IsOriginalCharacter);
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

        var restored = PlayerCharacterTemplate.RestoreHeroCultures(new[] { joined }, mainHero);

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

        var restored = PlayerCharacterTemplate.RestoreHeroCultures(new[] { wanderer }, Character("main_hero"));

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

        var restored = PlayerCharacterTemplate.RestoreHeroCultures(new[] { host }, mainHeroCharacter);

        Assert.Equal(0, restored);
        Assert.Same(xmlCulture, ((BasicCharacterObject)mainHeroCharacter).Culture);
    }

    [Fact]
    public void RestoreHeroCultures_HeroWithoutACulture_KeepsTheCharactersCulture()
    {
        var mainHero = Character("main_hero");
        var joined = HeroWith(Character("Player"));
        joined.CharacterObject._originCharacter = mainHero;
        var characterCulture = ObjectHelper.SkipConstructor<CultureObject>();
        joined.CharacterObject._culture = characterCulture;

        var restored = PlayerCharacterTemplate.RestoreHeroCultures(new[] { joined }, mainHero);

        Assert.Equal(0, restored);
        Assert.Same(characterCulture, ((BasicCharacterObject)joined.CharacterObject).Culture);
    }

    [Fact]
    public void RestoreHeroCultures_NoTemplateFound_ChangesNothing()
    {
        var joined = HeroWith(Character("Player"));
        joined.CharacterObject._originCharacter = Character("main_hero");
        joined.Culture = ObjectHelper.SkipConstructor<CultureObject>();

        var restored = PlayerCharacterTemplate.RestoreHeroCultures(new[] { joined }, null);

        Assert.Equal(0, restored);
        Assert.Null(((BasicCharacterObject)joined.CharacterObject).Culture);
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
