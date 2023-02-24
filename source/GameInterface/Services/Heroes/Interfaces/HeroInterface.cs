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
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    internal interface IHeroInterface : IGameAbstraction
    {
        void PackageMainHero();
        void ResolveHero(ResolveHero message);
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
            return package.Unpack<Hero>();
        }

        public void ResolveHero(ResolveHero message)
        {
            // TODO implement
            messageBroker.Publish(this, new ResolveDebugHero(message.TransactionId, message.PlayerId));
        }
    }
}
