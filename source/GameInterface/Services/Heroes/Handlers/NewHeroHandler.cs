using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class NewHeroHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

        private readonly IHeroInterface heroInterface;
        private readonly IMessageBroker messageBroker;

        public NewHeroHandler(
            IHeroInterface heroInterface,
            IMessageBroker messageBroker)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PackageMainHero>(Handle);
            messageBroker.Subscribe<RegisterNewPlayerHero>(Handle);
        }

        private void Handle(MessagePayload<PackageMainHero> obj)
        {
            try
            {
                heroInterface.PackageMainHero();
            }
            catch (Exception e)
            {
                Logger.Error("Error while packing new Hero: {error}", e.Message);
            }
        }

        private void Handle(MessagePayload<RegisterNewPlayerHero> obj)
        {
            var registrationId = obj.What.PeerId;
            byte[] bytes = obj.What.Bytes;

            try
            {
                Hero hero = heroInterface.UnpackMainHero(bytes);

                Logger.Information("New Hero ID: {id}", hero.Id.InternalValue);

                var registerMessage = new NewPlayerHeroRegistered(registrationId, hero);

                messageBroker.Publish(this, registerMessage);
            }
            catch(Exception e)
            {
                Logger.Error("Error while unpacking new Hero: {error}", e.Message);
            }
        }
    }
}
