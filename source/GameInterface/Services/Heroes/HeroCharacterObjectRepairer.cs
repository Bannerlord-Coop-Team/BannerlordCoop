using Serilog;
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

    public HeroCharacterObjectRepairer(ILogger logger, ICharacterObjectCreator characterObjectCreator)
    {
        this.logger = logger;
        this.characterObjectCreator = characterObjectCreator;
    }

    public bool TryRepair(Hero hero)
    {
        if (hero == null) throw new System.ArgumentNullException(nameof(hero));
        if (hero.CharacterObject != null) return false;

        var template = hero.Culture?.BasicTroop;
        if (template == null)
        {
            logger.Error("Unable to repair missing CharacterObject for hero {HeroId}: culture basic troop was unavailable", hero.StringId);
            return false;
        }

        var characterObject = characterObjectCreator.CreateFrom(template);
        characterObject.HeroObject = hero;
        hero._characterObject = characterObject;

        logger.Warning("Repaired missing CharacterObject for hero {HeroId} using {TemplateId}", hero.StringId, template.StringId);
        return true;
    }
}
