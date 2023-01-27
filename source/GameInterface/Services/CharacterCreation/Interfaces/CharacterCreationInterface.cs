using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.CharacterCreation.Messages;
using SandBox;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CharacterCreation.Interfaces
{
    internal interface ICharacterCreationInterface : IGameAbstraction
    {
        void PackageMainHero();
        void StartCharacterCreation();
    }

    internal class CharacterCreationInterface : ICharacterCreationInterface
    {
        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IMessageBroker messageBroker;

        public CharacterCreationInterface(IBinaryPackageFactory binaryPackageFactory, IMessageBroker messageBroker)
        {
            this.binaryPackageFactory = binaryPackageFactory;
            this.messageBroker = messageBroker;
        }

        public void PackageMainHero()
        {
            HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);
            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            messageBroker.Publish(this, new CharacterCreatedHeroPackaged(bytes));
        }

        public void StartCharacterCreation()
        {
            MBGameManager.StartNewGame(new SandBoxGameManager());
        }
    }
}
