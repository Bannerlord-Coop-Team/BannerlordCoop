using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.Heroes.Handlers;
using System;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.Save.Handler
{
    internal class SaveDataHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private NetworkGameSaveDataReceived saveDataMessage;
        public SaveDataHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            messageBroker.Subscribe<CampaignLoaded>(Handle_CampaignLoaded);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            messageBroker.Unsubscribe<CampaignLoaded>(Handle_CampaignLoaded);
        }

        private void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
        {
            saveDataMessage = obj.What;
        }

        private void Handle_CampaignLoaded(MessagePayload<CampaignLoaded> obj)
        {
            var message = new RegisterAllGameObjects();

            messageBroker.Publish(this, message);
        }
    }
}
