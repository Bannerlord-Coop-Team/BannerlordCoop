using Common;
using GameInterface.Services.Save;
using SandBox;
using System.Reflection;
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
            MBGameManager.EndGame();
        }

        private static readonly FieldInfo info_data = typeof(InMemDriver).GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
        public void LoadSaveGame(byte[] saveData)
        {
            GameLoopRunner.RunOnMainThread(() => InteralLoadSaveGame(saveData));
        }
        
        private void InteralLoadSaveGame(byte[] saveData)
        {
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
