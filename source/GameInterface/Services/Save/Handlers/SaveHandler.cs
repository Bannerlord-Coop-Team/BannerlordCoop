using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Save.Messages;
using System.Collections.Generic;
using System;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Save.Interfaces;

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
        }

        private void Handle(MessagePayload<PackageGameSaveData> obj)
        {
            var transactionId = obj.What.TransactionID;
            var gameData = saveInterface.SaveCurrentGame();

            var packagedMessage = new GameSaveDataPackaged(
                transactionId,
                gameData,
                Campaign.Current?.UniqueGameId,
                registryInterface.GetControlledHeroIds(),
                partyIds: registryInterface.GetPartyIds(),
                heroIds: registryInterface.GetHeroIds());

            messageBroker.Publish(this, packagedMessage);
        }

        private void Handle(MessagePayload<PackageObjectGuids> obj)
        {
            var transactionId = obj.What.TransactionID;
            var packagedMessage = new ObjectGuidsPackaged(
                transactionId,
                Campaign.Current?.UniqueGameId,
                registryInterface.GetControlledHeroIds(),
                partyIds: registryInterface.GetPartyIds(),
                heroIds: registryInterface.GetHeroIds());

            messageBroker.Publish(this, packagedMessage);
        }

        private void Handle(MessagePayload<LoadExistingObjectGuids> obj)
        {
            var payload = obj.What;
            registryInterface.LoadObjectGuids(
                payload.ControlledHeros,
                payload.HeroIds,
                payload.PartyIds);

            messageBroker.Publish(this, new ExistingObjectGuidsLoaded(payload.TransactionID));
        }
    }
}
