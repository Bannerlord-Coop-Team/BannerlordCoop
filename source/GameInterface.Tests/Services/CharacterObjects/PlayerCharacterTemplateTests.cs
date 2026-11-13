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
}
