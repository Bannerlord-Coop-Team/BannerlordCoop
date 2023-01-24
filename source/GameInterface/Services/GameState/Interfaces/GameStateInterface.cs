<<<<<<< HEAD
﻿using GameInterface.Services.Save;
=======
﻿using GameInterface.Data;
>>>>>>> NetworkEvent-refactor
using SandBox;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameState.Interfaces
{
<<<<<<< HEAD
    internal interface IGameStateInterface : IGameAbstraction
    {
        void EnterMainMenu();
        void StartNewGame();
        void LoadSaveGame(IGameSaveData saveData);
        IGameSaveData PackageGameSaveData();
        void EndGame();
    }

=======
>>>>>>> NetworkEvent-refactor
    internal class GameStateInterface : IGameStateInterface
    {
        public void EnterMainMenu()
        {
            MBGameManager.EndGame();
        }

        private static readonly FieldInfo info_data = typeof(InMemDriver).GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
        public void LoadSaveGame(IGameSaveData saveData)
        {
            ISaveDriver driver = new InMemDriver();
            info_data.SetValue(driver, saveData.Data);
            LoadResult loadResult = SaveManager.Load("", driver);
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
        }

        // TODO use in game pausing prefix
        // TODO add state for client joining
        public static bool IsGamePaused { get; private set; } = false;
        public void PauseGame()
        {
            IsGamePaused = true;
        }

        public void ResumeGame()
        {
            IsGamePaused = false;
        }

        private static readonly MethodInfo info_GetSaveMetaData = typeof(SaveHandler).GetMethod("GetSaveMetaData", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo info_GetSaveMetaData2 = typeof(MBSaveLoad).GetMethod("GetSaveMetaData", BindingFlags.NonPublic | BindingFlags.Static);
        public IGameSaveData PackageGameSaveData()
        {
            SaveHandler saveHandler = Campaign.Current.SaveHandler;
            object saveData = info_GetSaveMetaData.Invoke(saveHandler, null);
            
            MetaData metaData = (MetaData)info_GetSaveMetaData2.Invoke(null, new object[] { saveData }); ; ;
            ISaveDriver driver = new InMemDriver();
            SaveManager.Save(Game.Current, metaData, "MPSave", driver);

            byte[] saveBytes = (byte[])info_data.GetValue(driver);
            return new GameSaveData(saveBytes);
        }

<<<<<<< HEAD
        
=======
        public void StartCharacterCreation()
        {
            MBGameManager.StartNewGame(new SandBoxGameManager());
        }
>>>>>>> NetworkEvent-refactor

        public void StartNewGame()
        {
            MBGameManager.StartNewGame(new SandBoxGameManager());
        }
<<<<<<< HEAD

        public void EndGame()
        {
            MBGameManager.EndGame();
        }
=======
>>>>>>> NetworkEvent-refactor
    }
}
