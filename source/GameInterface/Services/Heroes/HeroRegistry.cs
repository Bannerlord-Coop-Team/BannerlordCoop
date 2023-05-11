using Common;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Registry;

internal interface IHeroRegistry : IRegistry<Hero>
{
    void RegisterAllHeroes();
}

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

        IEnumerable<Hero> heroes = campaignObjectManager.AliveHeroes.Concat(campaignObjectManager.DeadOrDisabledHeroes);
        foreach (var hero in heroes)
        {
            if (RegisterExistingObject(hero.StringId, hero) == false)
            {
                Logger.Warning("Unable to register hero: {object}", hero.Name);
            }
        }
    }
}
