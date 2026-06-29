using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Workshops.Messages;

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
        messageBroker.Publish(this, new InitializeClientWorkshopData(saveDataMessage.WorkshopPlayerData));
        messageBroker.Publish(this, new InitializeClientCaravansData(saveDataMessage.CaravansPlayerData));
        messageBroker.Publish(this, new InitializeClientInteractionsData(saveDataMessage.InteractionsPlayerData));
        // Add any other CoopSession data initialisations for clients here
    }
}
