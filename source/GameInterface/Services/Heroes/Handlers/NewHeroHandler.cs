using Common.Messaging;
using GameInterface.Services.CharacterCreation.Interfaces;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class NewHeroHandler : IHandler
    {
        private readonly IHeroInterface heroInterface;
        private readonly IMessageBroker messageBroker;

        public NewHeroHandler(
            IHeroInterface heroInterface,
            IMessageBroker messageBroker)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PackageMainHero>(Handle);
            messageBroker.Subscribe<NewPlayerHeroRecieved>(Handle);
        }

        private void Handle(MessagePayload<PackageMainHero> obj)
        {
            heroInterface.PackageMainHero();
        }

        private void Handle(MessagePayload<NewPlayerHeroRecieved> obj)
        {
            byte[] bytes = obj.What.Bytes;

            Hero hero = heroInterface.UnpackMainHero(bytes);

            messageBroker.Publish(obj.Who, new NewPlayerHeroRegistered(hero.Id));
        }
    }
}
