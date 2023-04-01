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
            IControlledHeroRegistry controlledHeroRegistry)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;
            this.heroRegistry = heroRegistry;
            this.controlledHeroRegistry = controlledHeroRegistry;

            messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
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
