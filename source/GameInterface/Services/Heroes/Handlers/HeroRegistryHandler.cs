using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class HeroRegistryHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

        private readonly IHeroInterface heroInterface;
        private readonly IMessageBroker messageBroker;
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledHeroRegistry controlledHeroRegistry;

        public HeroRegistryHandler(
            IHeroInterface heroInterface,
            IMessageBroker messageBroker,
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroesRegistry)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;
            this.heroRegistry = heroRegistry;
            this.controlledHeroRegistry = controlledHeroesRegistry;

            messageBroker.Subscribe<RegisterHeroes>(Handle_RegisterHeroes);
            messageBroker.Subscribe<RegisterHeroesWithStringIds>(Handle_RegisterHeroesWithStringIds);
            messageBroker.Subscribe<RegisterExistingControlledHeroes>(Handle_RegisterExistingControlledHeroes);
            messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        }

        

        private void Handle_RegisterHeroes(MessagePayload<RegisterHeroes> obj)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null) return;

            IEnumerable<Hero> heroes = Enumerable.Concat(objectManager.AliveHeroes, objectManager.DeadOrDisabledHeroes);
            foreach (var hero in heroes)
            {
                heroRegistry.RegisterNewObject(hero);
            }

            messageBroker.Publish(this, new PartiesRegistered());
        }

        private void Handle_RegisterHeroesWithStringIds(MessagePayload<RegisterHeroesWithStringIds> obj)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null) return;

            var stringIdToGuidDict = obj.What.AssociatedStringIdValues;

            // Error recording lists
            var unregisteredParties = new List<string>();
            var badGuidParties = new List<string>();

            IEnumerable<Hero> heroes = Enumerable.Concat(objectManager.AliveHeroes, objectManager.DeadOrDisabledHeroes);
            foreach (var hero in heroes)
            {
                if (stringIdToGuidDict.TryGetValue(hero.StringId, out Guid id))
                {
                    if (id != Guid.Empty)
                    {
                        heroRegistry.RegisterExistingObject(id, hero);
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

            var transactionId = obj.What.TransactionID;
            messageBroker.Publish(this, new HeroesRegistered(transactionId));
        }

        private void Handle_RegisterExistingControlledHeroes(MessagePayload<RegisterExistingControlledHeroes> obj)
        {
            var controlledHeros = obj.What.ControlledHeroIds;

            foreach (var id in controlledHeros)
            {
                if (controlledHeroRegistry.RegisterAsControlled(id) == false)
                {
                    Logger.Error($"Unable to add {id} to ControlledHeroRegistry");
                }
            }
        }
        private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
        {
            var previousHero = obj.What.PreviousHero;
            var newHero = obj.What.NewHero;

            if (heroRegistry.TryGetValue(previousHero, out Guid previousHeroId))
            {
                controlledHeroRegistry.RemoveAsControlled(previousHeroId);
            }

            if (heroRegistry.TryGetValue(newHero, out Guid newHeroId))
            {
                controlledHeroRegistry.RemoveAsControlled(newHeroId);
            }
        }
    }
}
