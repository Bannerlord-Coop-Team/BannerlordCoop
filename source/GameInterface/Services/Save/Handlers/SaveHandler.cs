using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Save.Messages;
using System.Collections.Generic;
using System;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Save.Interfaces;
using GameInterface.Services.Heroes.Messages;
using System.Threading.Tasks;
using GameInterface.Services.MobileParties.Messages;
using TaleWorlds.ObjectSystem;
using GameInterface.Services.Save.Data;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class SaveHandler : IHandler
    {
        private readonly ISaveInterface saveInterface;
        private readonly IRegistryInterface registryInterface;
        private readonly IMessageBroker messageBroker;

        public SaveHandler(
            ISaveInterface saveInterface,
            IRegistryInterface registryInterface,
            IMessageBroker messageBroker)
        {
            this.saveInterface = saveInterface;
            this.registryInterface = registryInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PackageGameSaveData>(Handle);
            messageBroker.Subscribe<PackageObjectGuids>(Handle);
            messageBroker.Subscribe<LoadExistingObjectGuids>(Handle);
            messageBroker.Subscribe<RegisterAllGameObjects>(Handle);
        }

        private void Handle(MessagePayload<PackageGameSaveData> obj)
        {
            var transactionId = obj.What.TransactionID;
            var gameData = saveInterface.SaveCurrentGame();

            var gameObjectGuids = new GameObjectGuids(
                registryInterface.GetControlledHeroIds(),
                partyIds: registryInterface.GetPartyIds(),
                heroIds: registryInterface.GetHeroIds());

            var packagedMessage = new GameSaveDataPackaged(
                transactionId,
                gameData,
                Campaign.Current?.UniqueGameId,
                gameObjectGuids);

            messageBroker.Publish(this, packagedMessage);
        }

        private void Handle(MessagePayload<PackageObjectGuids> obj)
        {
            var transactionId = obj.What.TransactionID;
            var gameObjectGuids = new GameObjectGuids(
                registryInterface.GetControlledHeroIds(),
                registryInterface.GetPartyIds(),
                registryInterface.GetHeroIds());

            var packagedMessage = new ObjectGuidsPackaged(
                transactionId,
                Campaign.Current?.UniqueGameId,
                gameObjectGuids);

            messageBroker.Publish(this, packagedMessage);
        }

        private void Handle(MessagePayload<LoadExistingObjectGuids> obj)
        {
            var payload = obj.What;
            registryInterface.LoadObjectGuids(payload.GameObjectGuids);

            messageBroker.Publish(this, new ExistingObjectGuidsLoaded(payload.TransactionID));
        }

        private void Handle(MessagePayload<RegisterAllGameObjects> obj)
        {
            var payload = obj.What;

            registryInterface.RegisterAllGameObjects();

            messageBroker.Publish(this, new AllGameObjectsRegistered(payload.TransactionID));
        }
    }
}
