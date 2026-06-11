using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Smithing.Messages;

namespace Coop.Core.Client.Services.Save.Handler;

/// <summary>
/// Handles save data
/// </summary>
internal class SaveDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private NetworkGameSaveDataReceived saveDataMessage;

    public SaveDataHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
    }

    private void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
    {
        // The loading screen is intentionally NOT ended here: save data has only just
        // arrived and the server world has not loaded yet. It is ended once the campaign
        // is ready (see LoadingState.Handle_CampaignLoaded).
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
