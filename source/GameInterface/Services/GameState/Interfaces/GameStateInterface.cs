using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes;
using GameInterface.Services.Modules;
using GameInterface.Services.UI.Interfaces;
using SandBox;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameState.Interfaces;

public interface IGameStateInterface : IGameAbstraction
{
    void GoToMainMenu();
    void StartNewGame();
    void LoadSaveData(byte[] saveData);
    void LoadGame(string saveName);

    /// <summary>
    /// Loads the named save, or starts a new campaign when no save has that name.
    /// </summary>
    void LoadGameOrStartNew(string saveName);
    void EndGame();
}

internal class GameStateInterface : IGameStateInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameStateInterface>();

    private readonly IMessageBroker messageBroker;
    private readonly Action endGame;
    private readonly INavalSaveCompatibility navalSaveCompatibility;
    private readonly ILoadingInterface loadingInterface;

    public GameStateInterface(IMessageBroker messageBroker, INavalSaveCompatibility navalSaveCompatibility,
        ILoadingInterface loadingInterface)
        : this(messageBroker, MBGameManager.EndGame, navalSaveCompatibility, loadingInterface)
    {
    }

    internal GameStateInterface(IMessageBroker messageBroker, Action endGame,
        INavalSaveCompatibility navalSaveCompatibility, ILoadingInterface loadingInterface)
    {
        this.messageBroker = messageBroker;
        this.endGame = endGame;
        this.navalSaveCompatibility = navalSaveCompatibility;
        this.loadingInterface = loadingInterface;
    }

    public void GoToMainMenu()
    {
        if (Campaign.Current == null) return;
        if (Game.Current == null) return;

        EndGame();
    }

    public void LoadSaveData(byte[] saveData)
    {
        messageBroker.Publish(this, new GameLoadStarted());
        GameThread.Run(() => InteralLoadSaveGame(saveData), blocking: true);
    }

    private void InteralLoadSaveGame(byte[] saveData)
    {
        if (saveData == null) throw new ArgumentNullException($"Received save data was null");

        if (GameStateManager.Current == null)
        {
            GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
        }

        ISaveDriver driver = new CoopInMemSaveDriver(saveData);
        LoadResult loadResult;

        // SaveManager fills objects on NativeParallelDriver workers. A normal AllowedThread scope only
        // affects this game thread, leaving AutoSync patches active on those workers. Keep every
        // CallOriginalPolicy-gated patch on its original path until the synchronous load joins its workers.
        using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
        {
            loadResult = SaveManager.Load("", driver, loadAsLateInitialize: true);
        }

        var loadedGame = (Game)loadResult.Root;
        var loadedCampaign = (Campaign)loadedGame.GameType;
        ClearTransferredMapNotices(loadedCampaign.CampaignInformationManager);
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
    }

    internal static void ClearTransferredMapNotices(CampaignInformationManager informationManager)
    {
        // A headless server cannot dismiss map notices, so its save contains the campaign's accumulated
        // UI backlog. Only network-transferred saves use this load path; live notices remain intact.
        informationManager._mapNotices.Clear();
    }

    public void StartNewGame()
    {
        messageBroker.Publish(this, new GameLoadStarted());
        GameThread.RunSafe(StartNewCampaign, context: nameof(StartNewGame));
    }

    public void LoadGame(string saveName)
    {
        LoadSave(saveName, () => Logger.Error("Failed to load save with name {SaveName}", saveName));
    }

    public void LoadGameOrStartNew(string saveName)
    {
        LoadSave(saveName, () =>
        {
            Logger.Information("No save named {SaveName}, starting a new campaign", saveName);
            StartNewCampaign();
        });
    }

    // Loads the named save on the game thread, blocking until it started, or runs onMissing there when no save has that name.
    private void LoadSave(string saveName, Action onMissing)
    {
        messageBroker.Publish(this, new GameLoadStarted());
        GameThread.Run(() =>
        {
            var save = MBSaveLoad.GetSaveFiles(null).SingleOrDefault(x => x.Name == saveName);

            if (save == null)
            {
                onMissing();
                return;
            }

            if (RefuseNavalMismatch(save)) return;

            ForceLoadSave(save);
        }, blocking: true);
    }

    // The forced load below skips vanilla's NavalDLC added/removed check, so a mismatched save is refused here instead.
    internal bool RefuseNavalMismatch(SaveGameFileInfo save)
    {
        string refusal = navalSaveCompatibility.GetHostRefusal(save.Name, save.MetaData.GetModules());
        if (refusal == null) return false;

        Logger.Error("Refusing to host: {Reason}", refusal);
        loadingInterface.HideLoadingScreen();
        InformationManager.DisplayMessage(new InformationMessage(refusal));
        return true;
    }

    // Starts a new sandbox campaign; game thread only.
    private static void StartNewCampaign()
    {
        MBGameManager.StartNewGame(new SandBoxGameManager(() => new Campaign(CampaignGameMode.Campaign)));
    }

    private void ForceLoadSave(SaveGameFileInfo save)
    {
        LogModuleCompatibilityMismatches(save);

        SandBoxSaveHelper.LoadGameAction(save, StartGame, null);
    }

    private static void LogModuleCompatibilityMismatches(SaveGameFileInfo save)
    {
        try
        {
            foreach (var mismatch in SandBoxSaveHelper.CheckMetaDataCompatibilityErrors(save.MetaData))
            {
                Logger.Warning(
                    "Save {SaveName} module mismatch: {ModuleId} ({MismatchType}). Forcing load anyway.",
                    save.Name,
                    mismatch.ModuleId,
                    mismatch.Type);
            }
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Unable to evaluate module compatibility for save {SaveName}. Forcing load anyway.", save.Name);
        }
    }

    private void StartGame(LoadResult loadResult)
    {
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        MouseManager.ShowCursor(false);
    }

    public void EndGame()
    {
        // A disconnect handled on the poller must still leave the campaign when this call expires before it starts.
        GameThread.RunCleanupSafe(endGame);
        // MBGameManager.EndGame is async void, so returning here does not mean InitialState is
        // active yet. MainMenuEnteredPatch publishes once InitialState.OnActivate actually runs.
    }
}
