using Common;
using System.Linq;
using TaleWorlds.CampaignSystem;

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
    public override bool RegisterNewObject(object obj, out string id) => RegisterNewObject(obj, HeroStringIdPrefix, out id);
}
