using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.CharacterCreation.Messages;
using Serilog;
using Serilog.Core;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    internal interface IHeroInterface : IGameAbstraction
    {
        void PackageMainHero();
        Hero UnpackMainHero(byte[] bytes);
    }

    internal class HeroInterface : IHeroInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();

        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IMessageBroker messageBroker;

        public HeroInterface(IBinaryPackageFactory binaryPackageFactory, IMessageBroker messageBroker)
        {
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
            var hero = package.Unpack<Hero>();
            MBObjectManager.Instance.RegisterObject(hero.PartyBelongedTo);
            MBObjectManager.Instance.RegisterObject(hero.CharacterObject);
            MBObjectManager.Instance.RegisterObject(hero.Clan);
            return MBObjectManager.Instance.RegisterObject(hero);
        }
    }
}
