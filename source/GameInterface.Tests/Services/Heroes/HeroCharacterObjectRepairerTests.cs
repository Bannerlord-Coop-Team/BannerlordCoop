using Common.Util;
using GameInterface.Services.Heroes;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class HeroCharacterObjectRepairerTests
{
    private readonly Mock<ILogger> logger = new();

    [Fact]
    public void TryRepair_MissingCharacterObject_DefersRegistrationAndRestoresBothReferences()
    {
        var template = ObjectHelper.SkipConstructor<CharacterObject>();
        var replacement = ObjectHelper.SkipConstructor<CharacterObject>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.BasicTroop = template;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.Culture = culture;
        var characterObjectCreator = new FakeCharacterObjectCreator(replacement);
        var repairer = CreateRepairer(characterObjectCreator);

        var repaired = repairer.TryRepair(hero);

        Assert.True(repaired);
        Assert.Null(characterObjectCreator.InitializationTemplate);
        Assert.Same(replacement, hero.CharacterObject);
        Assert.Same(hero, replacement.HeroObject);
        Assert.Equal(
            $"{HeroCharacterObjectRepairer.DeferredCharacterObjectPrefix}{hero.StringId}",
            characterObjectCreator.StringId);
    }

    [Fact]
    public void TryRepair_ExistingCharacterObject_DoesNothing()
    {
        var existing = ObjectHelper.SkipConstructor<CharacterObject>();
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._characterObject = existing;
        var characterObjectCreator = new FakeCharacterObjectCreator(ObjectHelper.SkipConstructor<CharacterObject>());
        var repairer = CreateRepairer(characterObjectCreator);

        var repaired = repairer.TryRepair(hero);

        Assert.False(repaired);
        Assert.Same(existing, hero.CharacterObject);
        Assert.Null(characterObjectCreator.StringId);
    }

    [Fact]
    public void TryRepair_MissingCultureTemplate_CreatesDeferredCharacterObject()
    {
        var replacement = ObjectHelper.SkipConstructor<CharacterObject>();
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.StringId = "Created_2878";
        var characterObjectCreator = new FakeCharacterObjectCreator(replacement);
        var repairer = CreateRepairer(characterObjectCreator);

        var repaired = repairer.TryRepair(hero);

        Assert.True(repaired);
        Assert.Same(replacement, hero.CharacterObject);
        Assert.Same(hero, replacement.HeroObject);
        Assert.Equal(
            $"{HeroCharacterObjectRepairer.DeferredCharacterObjectPrefix}{hero.StringId}",
            characterObjectCreator.StringId);
        Assert.Null(characterObjectCreator.InitializationTemplate);
    }

    [Fact]
    public void TryHydrate_DeferredCharacterObject_InitializesFromLoadedCulture()
    {
        var template = ObjectHelper.SkipConstructor<CharacterObject>();
        var replacement = ObjectHelper.SkipConstructor<CharacterObject>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.BasicTroop = template;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.StringId = "Created_2878";
        var cultures = new List<CultureObject>();
        var characterObjectCreator = new FakeCharacterObjectCreator(replacement);
        var repairer = CreateRepairer(characterObjectCreator, cultures);

        Assert.True(repairer.TryRepair(hero));
        cultures.Add(culture);

        var hydrated = repairer.TryHydrate(hero);

        Assert.True(hydrated);
        Assert.Same(template, characterObjectCreator.InitializationTemplate);
        Assert.Same(replacement, characterObjectCreator.InitializedCharacterObject);
        Assert.Same(culture, hero.Culture);
        Assert.False(repairer.TryHydrate(hero));
    }

    [Fact]
    public void TryHydrate_NormalCharacterObject_DoesNothing()
    {
        var characterObject = ObjectHelper.SkipConstructor<CharacterObject>();
        characterObject.StringId = "normal_character";
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._characterObject = characterObject;
        var characterObjectCreator = new FakeCharacterObjectCreator(characterObject);
        var repairer = CreateRepairer(characterObjectCreator);

        var hydrated = repairer.TryHydrate(hero);

        Assert.False(hydrated);
        Assert.Null(characterObjectCreator.InitializationTemplate);
    }

    [Fact]
    public void TryHydrate_MissingHeroCulture_UsesClanCulture()
    {
        var template = ObjectHelper.SkipConstructor<CharacterObject>();
        var replacement = ObjectHelper.SkipConstructor<CharacterObject>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.BasicTroop = template;
        var clan = ObjectHelper.SkipConstructor<Clan>();
        clan.Culture = culture;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._clan = clan;
        var characterObjectCreator = new FakeCharacterObjectCreator(replacement);
        var repairer = CreateRepairer(characterObjectCreator);

        Assert.True(repairer.TryRepair(hero));
        var hydrated = repairer.TryHydrate(hero);

        Assert.True(hydrated);
        Assert.Same(culture, hero.Culture);
        Assert.Same(template, characterObjectCreator.InitializationTemplate);
    }

    [Fact]
    public void TryHydrate_MissingRelatedCulture_UsesLoadedCulture()
    {
        var template = ObjectHelper.SkipConstructor<CharacterObject>();
        var replacement = ObjectHelper.SkipConstructor<CharacterObject>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.BasicTroop = template;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var characterObjectCreator = new FakeCharacterObjectCreator(replacement);
        var repairer = CreateRepairer(characterObjectCreator, culture);

        Assert.True(repairer.TryRepair(hero));
        var hydrated = repairer.TryHydrate(hero);

        Assert.True(hydrated);
        Assert.Same(culture, hero.Culture);
        Assert.Same(template, characterObjectCreator.InitializationTemplate);
    }

    private HeroCharacterObjectRepairer CreateRepairer(
        ICharacterObjectCreator characterObjectCreator,
        params CultureObject[] cultures)
    {
        return CreateRepairer(characterObjectCreator, (IEnumerable<CultureObject>)cultures);
    }

    private HeroCharacterObjectRepairer CreateRepairer(
        ICharacterObjectCreator characterObjectCreator,
        IEnumerable<CultureObject> cultures)
    {
        return new HeroCharacterObjectRepairer(
            logger.Object,
            characterObjectCreator,
            new FakeCultureObjectProvider(cultures));
    }

    private sealed class FakeCultureObjectProvider : ICultureObjectProvider
    {
        private readonly IEnumerable<CultureObject> cultures;

        public FakeCultureObjectProvider(IEnumerable<CultureObject> cultures)
        {
            this.cultures = cultures;
        }

        public IEnumerable<CultureObject> GetAll()
        {
            return cultures ?? Array.Empty<CultureObject>();
        }
    }

    private sealed class FakeCharacterObjectCreator : ICharacterObjectCreator
    {
        private readonly CharacterObject replacement;

        public string? StringId { get; private set; }
        public CharacterObject? InitializedCharacterObject { get; private set; }
        public CharacterObject? InitializationTemplate { get; private set; }

        public FakeCharacterObjectCreator(CharacterObject replacement)
        {
            this.replacement = replacement;
        }

        public CharacterObject CreateUnregistered(string stringId)
        {
            StringId = stringId;
            replacement.StringId = stringId;
            return replacement;
        }

        public void RegisterAndInitializeFrom(CharacterObject characterObject, CharacterObject template)
        {
            InitializedCharacterObject = characterObject;
            InitializationTemplate = template;
            characterObject._originCharacter = template;
        }
    }
}
