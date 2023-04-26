using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Handlers;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using Serilog.Core;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    internal interface IHeroInterface : IGameAbstraction
    {
        void PackageMainHero();
        void ResolveHero(ResolveHero message);
        void SwitchMainHero(string heroId);
        Hero UnpackMainHero(byte[] bytes);
    }

    internal class HeroInterface : IHeroInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
        private readonly IHeroRegistry heroRegistry;
        private readonly IControlledHeroRegistry controlledHeroRegistry;
        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IMessageBroker messageBroker;

        public HeroInterface(
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroRegistry,
            IBinaryPackageFactory binaryPackageFactory,
            IMessageBroker messageBroker)
        {
            this.heroRegistry = heroRegistry;
            this.controlledHeroRegistry = controlledHeroRegistry;
            this.binaryPackageFactory = binaryPackageFactory;
            this.messageBroker = messageBroker;
        }

        public void PackageMainHero()
        {
            HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);
            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            messageBroker.Publish(this, new NewHeroPackaged(bytes));
        }

        public Hero UnpackMainHero(byte[] bytes)
        {
            HeroBinaryPackage package = BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(bytes);
            return package.Unpack<Hero>(binaryPackageFactory);
        }

        public void ResolveHero(ResolveHero message)
        {
            // TODO implement
            messageBroker.Publish(this, new ResolveDebugHero(message.TransactionID, message.PlayerId));
        }

        public void SwitchMainHero(string heroId)
        {
            if(heroRegistry.TryGetValue(heroId, out Hero resolvedHero))
            {
                Logger.Information("Switching to new hero: {heroName}", resolvedHero.Name.ToString());

                ChangePlayerCharacterAction.Apply(resolvedHero);
            }
            else
            {
                Logger.Warning("Could not find hero with id of: {guid}", heroId);
            }
        }
    }
}
