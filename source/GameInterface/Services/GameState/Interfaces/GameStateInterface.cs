using Common;
using GameInterface.Services.Heroes;
using Common.Logging;
using SandBox;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameState.Interfaces;

internal interface IGameStateInterface : IGameAbstraction
{
    void EnterMainMenu();
    void StartNewGame();
    void LoadSaveGame(byte[] saveData);
    void EndGame();
}

internal class GameStateInterface : IGameStateInterface
{
    private static readonly Serilog.ILogger Logger = LogManager.GetLogger<GameStateInterface>();
    public static bool IsLoadingGame { get; set; }
    public static DateTime? EnterMainMenuBlockedUntil { get; set; }
    public void EnterMainMenu()
    {
        // Guard against returning to menu while a blocking load is in progress.
        if (IsLoadingGame)
        {
            Logger.Warning("EnterMainMenu ignored: loading in progress");
            return;
        }
        // Prevent rapid re-entry to menu immediately after load to stabilize state changes.
        if (EnterMainMenuBlockedUntil.HasValue && DateTime.UtcNow < EnterMainMenuBlockedUntil.Value)
        {
            Logger.Warning("EnterMainMenu ignored: blocked until {BlockedUntil}", EnterMainMenuBlockedUntil);
            return;
        }
        if (Campaign.Current == null) return;
        if (Game.Current == null) return;

        // End the current game from the main thread to reset all states and screens.
        EndGame();
    }

    public void LoadSaveGame(byte[] saveData)
    {
        // Set loading flag to coordinate blocking calls and menu re-entry.
        IsLoadingGame = true;
        Logger.Information("LoadSaveGame invoked (bytes={Length})", saveData?.Length ?? 0);
        GameLoopRunner.RunOnMainThread(() => InteralLoadSaveGame(saveData), blocking: true);
    }

    private void InteralLoadSaveGame(byte[] saveData)
    {
        if (saveData == null) throw new ArgumentNullException($"Received save data was null");

        if (GameStateManager.Current == null)
        {
            GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
        }

        try
        {
            // Use in-memory save driver to load transferred data, then start a SandBox game with it.
            ISaveDriver driver = new CoopInMemSaveDriver(saveData);
            LoadResult loadResult = SaveManager.Load("", driver, loadAsLateInitialize: true);
            Logger.Information("Save loaded: Result={Result}", loadResult);
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
            Logger.Information("StartNewGame invoked with loaded save");
            Common.Messaging.MessageBroker.Instance.Publish(this, new global::GameInterface.Services.GameState.Messages.CampaignReady());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Load/StartNewGame failed");
        }
        finally
        {
            // Clear loading flag and block menu re-entry for a short period to avoid race conditions.
            IsLoadingGame = false;
            EnterMainMenuBlockedUntil = DateTime.UtcNow.AddSeconds(5);
        }
    }

    public void StartNewGame()
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            // Start an empty SandBox game to enter character creation flow.
            MBGameManager.StartNewGame(new SandBoxGameManager(default(LoadResult)));
        });
    }

    public void EndGame()
    {
        // Request Bannerlord to end current game; UI transitions happen on main thread.
        GameLoopRunner.RunOnMainThread(MBGameManager.EndGame);
    }
}
