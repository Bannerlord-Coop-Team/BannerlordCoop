using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.UI.Messages;

namespace Coop.Core.Client.Services.Save.Handler
{
    /// <summary>
    /// Handles save data
    /// </summary>
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
            messageBroker.Publish(this, new EndLoadingScreen()); // TODO update to work
            saveDataMessage = obj.What;

            messageBroker.Publish(this, new InitializeClientCraftingData(saveDataMessage.CraftingPlayerData));
            // Expand this in the future to handle other CoopSession data types if needed to look something like:
            /*
            ICoopSession CoopSession = saveDataMessage.CoopSession;
            messageBroker.Publish(this, new InitializeCraftingCampaignBehavior(CoopSession.CraftingPlayerData));
            // Add any other CoopSession data initialisations for clients
            */
        }
    }
}
