using Common.Messaging;
using GameInterface.Services.Save.Messages;
using System;

namespace Coop.Core.Server.Services.Save.Handlers
{
    internal class SaveGameHandler
    {
        private ICoopSaveManager _saveManager;

        public SaveGameHandler(IMessageBroker messageBroker, ICoopSaveManager saveManager) 
        {
            messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
            _saveManager = saveManager;
        }

        

        private void Handle_GameSaved(MessagePayload<GameSaved> obj)
        {
            string saveName = obj.What.SaveName;

            //_saveManager.SaveCoopSession(saveName, );
        }
    }
}
