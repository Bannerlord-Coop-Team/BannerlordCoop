using Common;
using Common.Logging;
using GameInterface.Services.Registry;
using Serilog;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes
{
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

            IEnumerable<Hero> heroes = Enumerable.Concat(campaignObjectManager.AliveHeroes, campaignObjectManager.DeadOrDisabledHeroes);
            foreach (var hero in heroes)
            {
                if(RegisterExistingObject(hero.StringId, hero) == false)
                {
                    Logger.Warning("Unable to register hero: {object}", hero.Name);
                }
            }
        }
    }
}
