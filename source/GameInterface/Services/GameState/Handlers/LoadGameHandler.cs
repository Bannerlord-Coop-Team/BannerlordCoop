using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.GameDebug.Messages;
using SandBox;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using System.Linq;
using System.IO;

namespace GameInterface.Services.GameState.Handlers;

internal class LoadGameHandler : IHandler
{
    private readonly IGameStateInterface gameStateInterface;
    private readonly IMessageBroker messageBroker;

    public LoadGameHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
    {
        this.gameStateInterface = gameStateInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<LoadGameSave>(Handle);
        messageBroker.Subscribe<LoadGame>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadGameSave>(Handle);
        messageBroker.Unsubscribe<LoadGame>(Handle);
    }

    private void Handle(MessagePayload<LoadGameSave> obj)
    {
        gameStateInterface.LoadSaveGame(obj.What.SaveData);

        messageBroker.Publish(this, new GameSaveLoaded());
    }

    private void Handle(MessagePayload<LoadGame> obj)
    {
        var saveName = Path.GetFileNameWithoutExtension(obj.What.SaveName);
        messageBroker.Publish(this, new StartLoadingScreen());

        GameLoopRunner.RunOnMainThread(() =>
        {
            var saves = MBSaveLoad.GetSaveFiles(null);
            var mp_save = saves.FirstOrDefault(x => x.Name == saveName);
            if (mp_save == null)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Sauvegarde '{obj.What.SaveName}' introuvable"));
                return;
            }

            SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
        }, blocking: true);
    }

    private void StartGame(LoadResult loadResult)
    {
        InformationManager.DisplayMessage(new InformationMessage("Sauvegarde chargée, initialisation de la campagne"));
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        MouseManager.ShowCursor(false);
    }
}
