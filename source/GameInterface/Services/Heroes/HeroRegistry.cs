using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using System.Collections.Generic;
using System.Linq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Serilog;
using Common.Logging;

namespace GameInterface.Services.Heroes
{
    internal interface IHeroRegistry : IRegistryBase<Hero>
    {
        void RegisterAllHeroes();
        void RegisterHeroesWithStringIds(IReadOnlyDictionary<string, Guid> stringIdToGuids);
    }

    internal class HeroRegistry : RegistryBase<Hero>, IHeroRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroRegistry>();

        public void RegisterAllHeroes()
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            IEnumerable<Hero> heroes = Enumerable.Concat(objectManager.AliveHeroes, objectManager.DeadOrDisabledHeroes);
            foreach (var hero in heroes)
            {
                RegisterNewObject(hero);
            }
        }

        public void RegisterHeroesWithStringIds(IReadOnlyDictionary<string, Guid> stringIdToGuids)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null)
            {
                Logger.Error("CampaignObjectManager was null when trying to register heroes");
                return;
            }

            // Error recording lists
            var unregisteredParties = new List<string>();
            var badGuidParties = new List<string>();

            IEnumerable<Hero> heroes = Enumerable.Concat(objectManager.AliveHeroes, objectManager.DeadOrDisabledHeroes);
            foreach (var hero in heroes)
            {
                if (stringIdToGuids.TryGetValue(hero.StringId, out Guid id))
                {
                    if (id != Guid.Empty)
                    {
                        RegisterExistingObject(id, hero);
                    }
                    else
                    {
                        // Parties with empty guids
                        badGuidParties.Add(hero.StringId);
                    }
                }
                else
                {
                    // Existing parties that don't exist in stringIds
                    unregisteredParties.Add(hero.StringId);
                }
            }

            // Log any bad guids if they exist
            if (badGuidParties.IsEmpty() == false)
            {
                Logger.Error("The following parties had incorrect Guids: {parties}", badGuidParties);
            }

            // Log any unregistered parties if they exist
            if (unregisteredParties.IsEmpty() == false)
            {
                Logger.Error("The following parties were not registered: {parties}", unregisteredParties);
            }
        }
    }
}
