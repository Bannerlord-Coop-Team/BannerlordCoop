// Ignore Spelling: Guids

using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Services.Entity.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages;
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
            messageBroker.Subscribe<CampaignLoaded>(Handle_CampaignLoaded);

            messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
            messageBroker.Subscribe<ExistingObjectGuidsLoaded>(Handle_ExistingObjectGuidsLoaded);
        }

        

        public void Dispose()
        {
            messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
            messageBroker.Unsubscribe<ObjectGuidsPackaged>(Handle_ObjectGuidsPackaged);
            messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);
            messageBroker.Unsubscribe<CampaignLoaded>(Handle_CampaignLoaded);

            messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
            messageBroker.Unsubscribe<ExistingObjectGuidsLoaded>(Handle_ExistingObjectGuidsLoaded);
        }

        private string saveName;
        private void Handle_GameSaved(MessagePayload<GameSaved> obj)
        {
            saveName = obj.What.SaveName;
            messageBroker.Publish(this, new PackageObjectGuids());
        }

        private void Handle_ObjectGuidsPackaged(MessagePayload<ObjectGuidsPackaged> obj)
        {
            var payload = obj.What;
            CoopSession session = new CoopSession()
            {
                UniqueGameId = payload.UniqueGameId,
                GameObjectGuids = payload.GameObjectGuids,
            };

            saveManager.SaveCoopSession(saveName, session);
        }

        private ICoopSession savedSession;
        private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
        {
            savedSession = saveManager.LoadCoopSession(obj.What.SaveName);
        }

        private void Handle_CampaignLoaded(MessagePayload<CampaignLoaded> obj)
        {
            if (savedSession == null)
            {
                messageBroker.Publish(this, new RegisterAllGameObjects());
            }
            else
            {
                var message = new LoadExistingObjectGuids(savedSession.GameObjectGuids);
                messageBroker.Publish(this, message);
            }
        }

        private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
        {
            messageBroker.Publish(this, new SetRegistryOwnerId(coopServer.ServerId));
            messageBroker.Publish(this, new RegisterAllPartiesAsControlled());
            messageBroker.Publish(this, new EnableGameTimeControls());
            coopServer.AllowJoining();
        }

        private void Handle_ExistingObjectGuidsLoaded(MessagePayload<ExistingObjectGuidsLoaded> obj)
        {
            messageBroker.Publish(this, new EnableGameTimeControls());
            coopServer.AllowJoining();
        }
    }
}
