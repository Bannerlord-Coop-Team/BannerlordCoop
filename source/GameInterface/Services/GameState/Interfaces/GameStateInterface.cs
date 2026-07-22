using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes;
using SandBox;
using Serilog;
using System;
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
    void LoadSaveData(byte[] saveData);
    void LoadGame(string saveName);
    void EndGame();
}

internal class GameStateInterface : IGameStateInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameStateInterface>();

    private readonly IMessageBroker messageBroker;

    public GameStateInterface(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
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
        LoadResult loadResult = SaveManager.Load("", driver, loadAsLateInitialize: true);
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
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
            // Server loads cannot wait on vanilla's module-mismatch inquiry behind the loading screen.
            if (!MBSaveLoad.IsSaveGameFileExists(saveName))
            {
                Logger.Error("Failed to load save with name {SaveName}", saveName);
                return;
            }

            var loadResult = MBSaveLoad.LoadSaveGameData(saveName);
            if (loadResult == null) return;

            StartGame(loadResult);
        }, blocking: true);
    }

    private void StartGame(LoadResult loadResult)
    {
        MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        MouseManager.ShowCursor(false);
    }

    public void EndGame()
    {
        GameThread.Run(MBGameManager.EndGame, blocking: true);

        messageBroker.Publish(this, new MainMenuEntered());
    }
}
