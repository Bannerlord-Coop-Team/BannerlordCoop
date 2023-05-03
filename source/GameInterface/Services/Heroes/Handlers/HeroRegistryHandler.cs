﻿using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class HeroRegistryHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

        private readonly IHeroInterface heroInterface;
        private readonly IMessageBroker messageBroker;
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledEntityRegistery controlledEntityRegistery;

        public HeroRegistryHandler(
            IHeroInterface heroInterface,
            IMessageBroker messageBroker,
            IHeroRegistry heroRegistry,
            IControlledEntityRegistery controlledEntityRegistery)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;
            this.heroRegistry = heroRegistry;
            this.controlledEntityRegistery = controlledEntityRegistery;

            messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        }

        private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
        {
            var previousHero = obj.What.PreviousHero;
            var newHero = obj.What.NewHero;

            if (heroRegistry.TryGetValue(previousHero, out string previousHeroId))
            {
                controlledEntityRegistery.RemoveAsControlled(previousHeroId);
            }

            if (heroRegistry.TryGetValue(newHero, out string newHeroId))
            {
                controlledEntityRegistery.RegisterAsControlled(newHeroId);
            }
        }
    }
}
