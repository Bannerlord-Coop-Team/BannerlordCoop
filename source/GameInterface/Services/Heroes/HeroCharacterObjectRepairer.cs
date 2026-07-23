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
    bool TryHydrate(Hero hero);
}

/// <inheritdoc cref="IHeroCharacterObjectRepairer"/>
internal class HeroCharacterObjectRepairer : IHeroCharacterObjectRepairer
{
    internal const string DeferredCharacterObjectPrefix = "CoopMissingHero_";

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

        var characterObject = characterObjectCreator.CreateUnregistered(
            $"{DeferredCharacterObjectPrefix}{hero.StringId}");
        characterObject.HeroObject = hero;
        hero._characterObject = characterObject;

        logger.Warning("Repaired missing CharacterObject for hero {HeroId}; registration and template initialization are deferred until load initialization completes",
            hero.StringId);
        return true;
    }

    public bool TryHydrate(Hero hero)
    {
        if (hero == null) throw new System.ArgumentNullException(nameof(hero));

        var characterObject = hero.CharacterObject;
        if (characterObject == null ||
            characterObject.OriginalCharacter != null ||
            string.IsNullOrEmpty(characterObject.StringId) ||
            !characterObject.StringId.StartsWith(DeferredCharacterObjectPrefix, System.StringComparison.Ordinal))
        {
            return false;
        }

        var culture = FindUsableCulture(hero);
        var template = culture?.BasicTroop;
        if (template == null) return false;

        characterObjectCreator.RegisterAndInitializeFrom(characterObject, template);
        hero.Culture = culture;

        logger.Warning("Initialized repaired CharacterObject for hero {HeroId} using {TemplateId} from culture {CultureId}",
            hero.StringId,
            template.StringId,
            culture.StringId);
        return true;
    }

    private CultureObject FindUsableCulture(Hero hero)
    {
        var availableCultures = cultureObjectProvider.GetAll();
        return GetUsableCulture(hero.Culture)
            ?? GetUsableCulture(hero.Clan?.Culture)
            ?? GetUsableCulture(hero.OriginClan?.Culture)
            ?? GetUsableCulture(hero.CurrentSettlement?.Culture)
            ?? GetUsableCulture(hero.BornSettlement?.Culture)
            ?? availableCultures.FirstOrDefault(candidate =>
                candidate?.IsMainCulture == true && candidate.BasicTroop != null)
            ?? availableCultures.FirstOrDefault(candidate => candidate?.BasicTroop != null);
    }

    private static CultureObject GetUsableCulture(CultureObject culture)
    {
        return culture?.BasicTroop != null ? culture : null;
    }
}
