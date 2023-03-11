using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using System;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class SwitchHeroHandler : IHandler
    {
        private readonly IHeroInterface heroInterface;
        private readonly IMessageBroker messageBroker;

        public SwitchHeroHandler(IHeroInterface heroInterface, IMessageBroker messageBroker)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<SwitchToHero>(Handle);
        }

        private void Handle(MessagePayload<SwitchToHero> obj)
        {
            heroInterface.SwitchMainHero(obj.What.HeroId);
        }
    }
}
