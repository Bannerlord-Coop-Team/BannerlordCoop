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
    public void TryRepair_MissingCharacterObject_RestoresBothReferences()
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
        Assert.Same(template, characterObjectCreator.Template);
        Assert.Same(replacement, hero.CharacterObject);
        Assert.Same(hero, replacement.HeroObject);
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
        Assert.Null(characterObjectCreator.Template);
    }

    [Fact]
    public void TryRepair_MissingCultureTemplate_DoesNotCreateCharacterObject()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var characterObjectCreator = new FakeCharacterObjectCreator(ObjectHelper.SkipConstructor<CharacterObject>());
        var repairer = CreateRepairer(characterObjectCreator);

        var repaired = repairer.TryRepair(hero);

        Assert.False(repaired);
        Assert.Null(hero.CharacterObject);
        Assert.Null(characterObjectCreator.Template);
    }

    [Fact]
    public void TryRepair_MissingHeroCulture_UsesClanCulture()
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

        var repaired = repairer.TryRepair(hero);

        Assert.True(repaired);
        Assert.Same(culture, hero.Culture);
        Assert.Same(template, characterObjectCreator.Template);
    }

    [Fact]
    public void TryRepair_MissingRelatedCulture_UsesLoadedCulture()
    {
        var template = ObjectHelper.SkipConstructor<CharacterObject>();
        var replacement = ObjectHelper.SkipConstructor<CharacterObject>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.BasicTroop = template;
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var characterObjectCreator = new FakeCharacterObjectCreator(replacement);
        var repairer = CreateRepairer(characterObjectCreator, culture);

        var repaired = repairer.TryRepair(hero);

        Assert.True(repaired);
        Assert.Same(culture, hero.Culture);
        Assert.Same(template, characterObjectCreator.Template);
    }

    private HeroCharacterObjectRepairer CreateRepairer(
        ICharacterObjectCreator characterObjectCreator,
        params CultureObject[] cultures)
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

        public CharacterObject? Template { get; private set; }

        public FakeCharacterObjectCreator(CharacterObject replacement)
        {
            this.replacement = replacement;
        }

        public CharacterObject CreateFrom(CharacterObject template)
        {
            Template = template;
            return replacement;
        }
    }
}
