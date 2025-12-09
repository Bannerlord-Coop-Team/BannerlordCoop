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
        if (IsLoadingGame)
        {
            Logger.Warning("EnterMainMenu ignored: loading in progress");
            return;
        }
        if (EnterMainMenuBlockedUntil.HasValue && DateTime.UtcNow < EnterMainMenuBlockedUntil.Value)
        {
            Logger.Warning("EnterMainMenu ignored: blocked until {BlockedUntil}", EnterMainMenuBlockedUntil);
            return;
        }
        if (Campaign.Current == null) return;
        if (Game.Current == null) return;

        EndGame();
    }

    public void LoadSaveGame(byte[] saveData)
    {
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
            IsLoadingGame = false;
            EnterMainMenuBlockedUntil = DateTime.UtcNow.AddSeconds(5);
        }
    }

    public void StartNewGame()
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            MBGameManager.StartNewGame(new SandBoxGameManager(default(LoadResult)));
        });
    }

    public void EndGame()
    {
        GameLoopRunner.RunOnMainThread(MBGameManager.EndGame);
    }
}
