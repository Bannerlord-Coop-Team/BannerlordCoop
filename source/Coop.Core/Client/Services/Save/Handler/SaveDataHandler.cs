using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.UI.Messages;
using GameInterface.Registry;

namespace Coop.Core.Client.Services.Save.Handler
{
    /// <summary>
    /// Handles save data
    /// </summary>
    // TODO update to work
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
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
        }

        private void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
        {
            messageBroker.Publish(this, new EndLoadingScreen());
            saveDataMessage = obj.What;
            var guids = saveDataMessage.GameObjectGuids;
            if (guids != null)
            {
                RegistrySnapshot.ItemRosterOwners = guids.ItemRosterOwners;
            }
        }
    }
}
