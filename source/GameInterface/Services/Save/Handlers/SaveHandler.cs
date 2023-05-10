using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

internal class SaveHandler : IHandler
{
    private readonly ISaveInterface saveInterface;
    private readonly IMessageBroker messageBroker;

    public SaveHandler(
        ISaveInterface saveInterface,
        IMessageBroker messageBroker)
    {
        this.saveInterface = saveInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<PackageGameSaveData>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PackageGameSaveData>(Handle);
    }

    private void Handle(MessagePayload<PackageGameSaveData> obj)
    {
        var transactionId = obj.What.TransactionID;
        var gameData = saveInterface.SaveCurrentGame();

        var packagedMessage = new GameSaveDataPackaged(
            transactionId,
            gameData,
            Campaign.Current?.UniqueGameId);

        messageBroker.Publish(this, packagedMessage);
    }
}
