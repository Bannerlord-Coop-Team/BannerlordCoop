using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes;
using SandBox;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameState.Interfaces;

public interface IGameStateInterface : IGameAbstraction
{
    void GoToMainMenu();
    void StartNewGame();

    /// <summary>
    /// Loads a campaign from the host's save bytes. Returns false when the load failed, so the caller can
    /// abort the join instead of waiting on a campaign that will never become ready.
    /// </summary>
    bool LoadSaveData(byte[] saveData);

    void LoadGame(string saveName);
    void EndGame();
}

internal class GameStateInterface : IGameStateInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameStateInterface>();

    private readonly IMessageBroker messageBroker;
    private readonly Action endGame;

    public GameStateInterface(IMessageBroker messageBroker)
        : this(messageBroker, () => GameThread.Run(MBGameManager.EndGame, blocking: true))
    {
    }

    internal GameStateInterface(IMessageBroker messageBroker, Action endGame)
    {
        this.messageBroker = messageBroker;
        this.endGame = endGame;
    }

    public void GoToMainMenu()
    {
        if (Campaign.Current == null) return;
        if (Game.Current == null) return;

        EndGame();
    }

    public bool LoadSaveData(byte[] saveData)
    {
        messageBroker.Publish(this, new GameLoadStarted());

        // The drain logs and swallows a failing action, so this flag is how the caller learns whether the
        // campaign actually loaded — the same captured-local pattern other blocking marshals here use.
        bool loaded = false;
        GameThread.Run(() =>
        {
            InteralLoadSaveGame(saveData);
            loaded = true;
        }, blocking: true);

        return loaded;
    }

    private void InteralLoadSaveGame(byte[] saveData)
    {
        if (saveData == null) throw new ArgumentNullException($"Received save data was null");

        if (GameStateManager.Current == null)
        {
            GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
        }

        ISaveDriver driver = new CoopInMemSaveDriver(saveData);
        LoadResult loadResult = SaveManager.Load("", driver, loadAsLateInitialize: true);
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
        GameThread.Run(() =>
        {
            MBGameManager.StartNewGame(new SandBoxGameManager(() => new Campaign(CampaignGameMode.Campaign)));
        });
    }

    public void LoadGame(string saveName)
    {
        messageBroker.Publish(this, new GameLoadStarted());
        GameThread.Run(() =>
        {
            var save = MBSaveLoad.GetSaveFiles(null).SingleOrDefault(x => x.Name == saveName);

            if (save == null)
            {
                Logger.Error("Failed to load save with name {SaveName}", saveName);
                return;
            }

            ForceLoadSave(save);
        }, blocking: true);
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
        endGame();
        // MBGameManager.EndGame is async void, so returning here does not mean InitialState is
        // active yet. MainMenuEnteredPatch publishes once InitialState.OnActivate actually runs.
    }
}
