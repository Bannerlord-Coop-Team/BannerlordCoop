using Common;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Registry;


/// <summary>
/// Registry for identifying ownership of <see cref="Hero"/> objects
/// </summary>
internal interface IHeroRegistry : IRegistry<Hero>
{
    void RegisterAllHeroes();
}

/// <inheritdoc cref="IHeroRegistry"/>
internal class HeroRegistry : RegistryBase<Hero>, IHeroRegistry
{
    public void RegisterAllHeroes()
    {
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;

        if (campaignObjectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        var heroes = campaignObjectManager.AliveHeroes.Concat(campaignObjectManager.DeadOrDisabledHeroes).ToArray();
        foreach (var hero in heroes)
        {
            RegisterExistingObject(hero.StringId, hero);
        }
    }

    public static readonly string HeroStringIdPrefix = "CoopHero";
    protected override string GetNewId(Hero party)
    {
        party.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Hero>(HeroStringIdPrefix);
        return party.StringId;
    }
}
