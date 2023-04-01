using Common.Messaging;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Save.Data;
using GameInterface.Services.Save.Messages;
using System;

namespace Coop.Core.Server.Services.Save.Handlers
{
    internal class SaveGameHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ICoopSaveManager saveManager;
        private readonly ICoopServer coopServer;

        public SaveGameHandler(
            IMessageBroker messageBroker, 
            ICoopSaveManager saveManager,
            ICoopServer coopServer) 
        {
            this.messageBroker = messageBroker;
            this.saveManager = saveManager;
            this.coopServer = coopServer;

            messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
            messageBroker.Subscribe<ObjectGuidsPackaged>(Handle_ObjectGuidsPackaged);
            messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);

            messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
            messageBroker.Subscribe<ExistingObjectGuidsLoaded>(Handle_ExistingObjectGuidsLoaded);
        }

        private string saveName;
        private Guid packageObjectsTransactionId;
        private void Handle_GameSaved(MessagePayload<GameSaved> obj)
        {
            saveName = obj.What.SaveName;
            packageObjectsTransactionId = Guid.NewGuid();

            messageBroker.Publish(this, new PackageObjectGuids(packageObjectsTransactionId));
        }

        private void Handle_ObjectGuidsPackaged(MessagePayload<ObjectGuidsPackaged> obj)
        {
            var payload = obj.What;
            if(packageObjectsTransactionId == payload.TransactionID)
            {
                CoopSession session = new CoopSession()
                {
                    UniqueGameId = payload.UniqueGameId,
                    GameObjectGuids = payload.GameObjectGuids,
                };

                saveManager.SaveCoopSession(saveName, session);
            }
        }

        private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
        {
            string saveName = obj.What.SaveName;

            ICoopSession session = saveManager.LoadCoopSession(saveName);

            Action<MessagePayload<CampaignLoaded>> postLoadHandler = null;

            if (session == null)
            {
                postLoadHandler = (payload) =>
                {
                    messageBroker.Publish(this, new RegisterAllGameObjects());
                    messageBroker.Unsubscribe(postLoadHandler);
                };
            }
            else
            {
                var message = new LoadExistingObjectGuids(
                    Guid.Empty, /* Transaction Id not required */
                    session.GameObjectGuids);

                postLoadHandler = (payload) =>
                {
                    messageBroker.Publish(this, message);
                    messageBroker.Unsubscribe(postLoadHandler);
                };
            }

            messageBroker.Subscribe(postLoadHandler);
        }

        private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
        {
            coopServer.AllowJoining();
        }

        private void Handle_ExistingObjectGuidsLoaded(MessagePayload<ExistingObjectGuidsLoaded> obj)
        {
            coopServer.AllowJoining();
        }
    }
}
