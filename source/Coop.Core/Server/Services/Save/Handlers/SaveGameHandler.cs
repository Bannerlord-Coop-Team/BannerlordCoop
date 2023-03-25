using Common.Messaging;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Save.Messages;
using System;

namespace Coop.Core.Server.Services.Save.Handlers
{
    internal class SaveGameHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ICoopSaveManager saveManager;

        public SaveGameHandler(IMessageBroker messageBroker, 
            ICoopSaveManager saveManager) 
        {
            this.messageBroker = messageBroker;
            this.saveManager = saveManager;

            messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
            messageBroker.Subscribe<ObjectGuidsPackaged>(Handle_ObjectGuidsPackaged);
            messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);
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
                    ControlledHeroes = payload.ControlledHeros,
                    PartyStringIdToGuid = payload.PartyIds,
                    HeroStringIdToGuid = payload.HeroIds,
                };

                saveManager.SaveCoopSession(saveName, session);
            }
        }

        private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
        {
            string saveName = obj.What.SaveName;

            ICoopSession session = saveManager.LoadCoopSession(saveName);

            // TODO an event is needed to generate ids since they don't exist
            if (session == null) return;

            
        }
    }
}
