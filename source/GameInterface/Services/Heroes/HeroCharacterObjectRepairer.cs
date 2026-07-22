using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes;

/// <summary>
/// Restores a loadable character object for a hero whose saved reference is missing.
/// </summary>
internal interface IHeroCharacterObjectRepairer
{
    bool TryRepair(Hero hero);
}

/// <inheritdoc cref="IHeroCharacterObjectRepairer"/>
internal class HeroCharacterObjectRepairer : IHeroCharacterObjectRepairer
{
    private readonly ILogger logger;
    private readonly ICharacterObjectCreator characterObjectCreator;
    private readonly ICultureObjectProvider cultureObjectProvider;

    public HeroCharacterObjectRepairer(
        ILogger logger,
        ICharacterObjectCreator characterObjectCreator,
        ICultureObjectProvider cultureObjectProvider)
    {
        this.logger = logger;
        this.characterObjectCreator = characterObjectCreator;
        this.cultureObjectProvider = cultureObjectProvider;
    }

    public bool TryRepair(Hero hero)
    {
        if (hero == null) throw new System.ArgumentNullException(nameof(hero));
        if (hero.CharacterObject != null) return false;

        var availableCultures = cultureObjectProvider.GetAll();
        var culture = GetUsableCulture(hero.Culture)
            ?? GetUsableCulture(hero.Clan?.Culture)
            ?? GetUsableCulture(hero.OriginClan?.Culture)
            ?? GetUsableCulture(hero.CurrentSettlement?.Culture)
            ?? GetUsableCulture(hero.BornSettlement?.Culture)
            ?? availableCultures.FirstOrDefault(candidate =>
                candidate?.IsMainCulture == true && candidate.BasicTroop != null)
            ?? availableCultures.FirstOrDefault(candidate => candidate?.BasicTroop != null);

        var template = culture?.BasicTroop;
        if (template == null)
        {
            logger.Error("Unable to repair missing CharacterObject for hero {HeroId}: no culture basic troop was available", hero.StringId);
            return false;
        }

        var characterObject = characterObjectCreator.CreateFrom(template);
        characterObject.HeroObject = hero;
        hero._characterObject = characterObject;
        hero.Culture = culture;

        logger.Warning("Repaired missing CharacterObject for hero {HeroId} using {TemplateId} from culture {CultureId}",
            hero.StringId,
            template.StringId,
            culture.StringId);
        return true;
    }

    private static CultureObject GetUsableCulture(CultureObject culture)
    {
        return culture?.BasicTroop != null ? culture : null;
    }
}
