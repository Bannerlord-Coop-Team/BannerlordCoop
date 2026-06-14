using Common;
using Common.Logging;
using GameInterface.Services.UI.Interfaces;
using SandBox;
using Serilog;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameDebug.Interfaces;

public interface IDebugGameInterface : IGameAbstraction
{
    void LoadDebugGame();
    void LoadGame(string saveName);
}

internal class DebugGameInterface : IDebugGameInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<DebugGameInterface>();

    public static readonly string LOAD_GAME = "MP";
    private readonly ILoadingInterface loadingInterface;

    public DebugGameInterface(ILoadingInterface loadingInterface)
    {
        this.loadingInterface = loadingInterface;
    }

    public void LoadDebugGame()
    {
        loadingInterface.ShowLoadingScreen();
        GameLoopRunner.RunOnMainThread(InternalLoadDebugGame, false);
    }

    private void InternalLoadDebugGame()
    {
        SaveGameFileInfo mp_save = MBSaveLoad.GetSaveFiles(null).Single(x => x.Name == LOAD_GAME);
        SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
    }

    private void StartGame(LoadResult loadResult)
    {
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        MouseManager.ShowCursor(false);
    }

    public void LoadGame(string saveName)
    {
        SaveGameFileInfo mp_save = MBSaveLoad.GetSaveFiles(null).Single(x => x.Name == saveName);
        SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
    }
}
