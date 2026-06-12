using Common;
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
        GameLoopRunner.RunOnMainThread(() =>
        {
            var gameData = saveInterface.SaveCurrentGame();

            var packagedMessage = new GameSaveDataPackaged(
                gameData,
                Campaign.Current?.UniqueGameId);

            // Respond synchronously so the save is sent inside this same main-thread block,
            // before the game loop resumes and broadcasts world changes. A joining client
            // treats everything it receives before the save as already part of the snapshot;
            // letting a post-snapshot change reach it ahead of the save would drop that change.
            messageBroker.RespondSync(obj.Who, packagedMessage);
        });
    }
}
