using Common.Messaging;
using SandBox;
using GameInterface.Services.GameDebug.Messages;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.Save.Handlers;

internal class LoadByNameHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public LoadByNameHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<LoadGame>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadGame>(Handle);
    }

    private void Handle(MessagePayload<LoadGame> obj)
    {
        var saveName = obj.What.SaveName;
        InformationManager.DisplayMessage(new InformationMessage($"Chargement de '{saveName}'..."));

        var normalized = System.IO.Path.GetFileNameWithoutExtension(saveName);
        var mp_save = MBSaveLoad.GetSaveFiles(null).FirstOrDefault(x => x.Name == normalized);
        if (mp_save == null)
        {
            InformationManager.DisplayMessage(new InformationMessage($"Sauvegarde '{saveName}' introuvable"));
            return;
        }

        SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
    }

    private void StartGame(LoadResult loadResult)
    {
        InformationManager.DisplayMessage(new InformationMessage("Sauvegarde chargée, initialisation de la campagne"));
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        MouseManager.ShowCursor(false);
    }
}
