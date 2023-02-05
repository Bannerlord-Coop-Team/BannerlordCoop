using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Save;
using SandBox;
using System;
using System.Reflection;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameState.Interfaces
{
    internal interface IGameStateInterface : IGameAbstraction
    {
        void EnterMainMenu();
        void StartNewGame();
        void LoadSaveGame(byte[] saveData);
        void EndGame();
    }

    internal class GameStateInterface : IGameStateInterface
    {
        public void EnterMainMenu()
        {
            EndGame();
        }

        public void LoadSaveGame(byte[] saveData)
        {
            GameLoopRunner.RunOnMainThread(() => InteralLoadSaveGame(saveData));
        }
        
        private void InteralLoadSaveGame(byte[] saveData)
        {
            if (saveData == null) throw new ArgumentNullException($"Received save data was null");

            ISaveDriver driver = new CoopInMemSaveDriver(saveData);
            LoadResult loadResult = SaveManager.Load("", driver);
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        }

        public void StartNewGame()
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                MBGameManager.StartNewGame(new SandBoxGameManager());
            });
        }

        public void EndGame()
        {
            GameLoopRunner.RunOnMainThread(MBGameManager.EndGame);
        }
    }
}
