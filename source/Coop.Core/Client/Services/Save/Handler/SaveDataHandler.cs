using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.Entity.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.Save.Handler
{
    /// <summary>
    /// Handles save data
    /// </summary>
    /// TODO update to work
    internal class SaveDataHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ICoopClient coopClient;
        private NetworkGameSaveDataReceived saveDataMessage;
        public SaveDataHandler(ICoopClient coopClient, IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            this.coopClient = coopClient;

            messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            messageBroker.Subscribe<CampaignReady>(Handle_CampaignLoaded);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignLoaded);
        }

        private void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
        {
            saveDataMessage = obj.What;
        }

        private void Handle_CampaignLoaded(MessagePayload<CampaignReady> obj)
        {
            messageBroker.Publish(this, new SetRegistryOwnerId(coopClient.ClientId));
            messageBroker.Publish(this, new RegisterAllGameObjects());
        }
    }
}
