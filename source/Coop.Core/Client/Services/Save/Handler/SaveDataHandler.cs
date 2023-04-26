using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Save.Messages;
using System;

namespace Coop.Core.Client.Services.Save.Handler
{
    internal class SaveDataHandler : IHandler
    {
        private readonly INetworkMessageBroker networkMessageBroker;
        private NetworkGameSaveDataReceived saveDataMessage;
        private bool saveDataRecieve = false;
        public SaveDataHandler(INetworkMessageBroker networkMessageBroker)
        {
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            networkMessageBroker.Subscribe<CampaignLoaded>(Handle_CampaignLoaded);
        }

        private void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
        {
            saveDataMessage = obj.What;
            saveDataRecieve = true;
        }

        private void Handle_CampaignLoaded(MessagePayload<CampaignLoaded> obj)
        {
            if(saveDataRecieve)
            {
                var message = new LoadExistingObjectGuids(
                Guid.Empty, /* Transaction Id not required */
                saveDataMessage.GameObjectGuids);

                networkMessageBroker.Publish(this, message);
            }
        }
    }
}
