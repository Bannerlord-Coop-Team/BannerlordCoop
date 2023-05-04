using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Save.Data;
using GameInterface.Services.Save.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class SaveHandler : IHandler
    {
        private readonly ISaveInterface saveInterface;
        private readonly IMessageBroker messageBroker;

        public SaveHandler(
            ISaveInterface saveInterface,
            IMessageBroker messageBroker)
        {
            this.saveInterface = saveInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PackageGameSaveData>(Handle);
            messageBroker.Subscribe<PackageObjectGuids>(Handle);
            messageBroker.Subscribe<LoadExistingObjectGuids>(Handle);
            messageBroker.Subscribe<RegisterAllGameObjects>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PackageGameSaveData>(Handle);
            messageBroker.Unsubscribe<PackageObjectGuids>(Handle);
            messageBroker.Unsubscribe<LoadExistingObjectGuids>(Handle);
            messageBroker.Unsubscribe<RegisterAllGameObjects>(Handle);
        }

        private void Handle(MessagePayload<PackageGameSaveData> obj)
        {
            //var transactionId = obj.What.TransactionID;
            //var gameData = saveInterface.SaveCurrentGame();

            //var gameObjectGuids = new GameObjectGuids(
            //    registryInterface.GetControlledHeroIds());

            //var packagedMessage = new GameSaveDataPackaged(
            //    transactionId,
            //    gameData,
            //    Campaign.Current?.UniqueGameId,
            //    gameObjectGuids);

            //messageBroker.Publish(this, packagedMessage);
        }

        private void Handle(MessagePayload<PackageObjectGuids> obj)
        {
            //var transactionId = obj.What.TransactionID;
            //var gameObjectGuids = new GameObjectGuids(
            //    registryInterface.GetControlledHeroIds());

            //var packagedMessage = new ObjectGuidsPackaged(
            //    transactionId,
            //    Campaign.Current?.UniqueGameId,
            //    gameObjectGuids);

            //messageBroker.Publish(this, packagedMessage);
        }

        private void Handle(MessagePayload<LoadExistingObjectGuids> obj)
        {
            //var payload = obj.What;
            //registryInterface.RegisterControlledHeroes(payload.GameObjectGuids.ControlledHeros);

            //messageBroker.Publish(this, new ExistingObjectGuidsLoaded(payload.TransactionID));
        }

        private void Handle(MessagePayload<RegisterAllGameObjects> obj)
        {
            var payload = obj.What;

            // TODO reimplement
            //registryInterface.RegisterAllGameObjects();

            messageBroker.Publish(this, new AllGameObjectsRegistered(payload.TransactionID));
        }
    }
}
