using System.Collections.Generic;
using System.Reflection;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Module = TaleWorlds.MountAndBlade.Module;

namespace GameInterface.Helpers
{
    public class SaveLoadHelper : ISaveLoadHelper
    {

        private static readonly string LOAD_GAME = "MP";
        public SaveGameData SaveGame(Game game, ISaveDriver driver)
        {

            EntitySystem<GameHandler> entitySystem = (EntitySystem<GameHandler>)typeof(Game)
                .GetField("_gameEntitySystem", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(game);

            MetaData metaData = GetMetaData();

            foreach (GameHandler gameHandler in entitySystem.Components)
            {
                gameHandler.OnBeforeSave();
            }

            SaveOutput saveOutput = SaveManager.Save(game, metaData, LOAD_GAME, driver);
            saveOutput.PrintStatus();
            foreach (GameHandler gameHandler2 in entitySystem.Components)
            {
                gameHandler2.OnAfterSave();
            }

            return new SaveGameData(metaData, saveOutput);
        }

        public void LoadGame(LoadResult loadResult)
        {
            if (Game.Current != null)
            {
                GameStateManager.Current.CleanStates();
                GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
            }

            //MBGameManager.StartNewGame(new ClientManager(loadResult));
        }

        public LoadResult LoadSaveGameData(ISaveDriver driver)
        {
            List<ModuleInfo> currentModules = GetModules();
            LoadResult loadResult = SaveManager.Load(LOAD_GAME, driver, true);
            if (loadResult.Successful)
            {
                return loadResult;
            }

            //Logger.Error("{loadResult}", loadResult.ToFriendlyString());

            return null;
        }

        //private static List<ModuleCheckResult> CheckModules(
        //    MetaData fileMetaData,
        //    List<ModuleInfo> loadedModules)
        //{
        //    return Utils.InvokePrivateMethod<List<ModuleCheckResult>>(
        //        typeof(MBSaveLoad),
        //        "CheckModules",
        //        null,
        //        new object[] {fileMetaData, loadedModules});
        //}

        private List<ModuleInfo> GetModules()
        {
            return ReflectionUtils.InvokePrivateMethod<List<ModuleInfo>>(
                typeof(ModuleHelper),
                "GetPhysicalModules",
                null,
                new object[] {});
        }

        private static MetaData GetMetaData()
        {
            CampaignSaveMetaDataArgs args = ReflectionUtils.InvokePrivateMethod<CampaignSaveMetaDataArgs>(
                typeof(SaveHandler),
                "GetSaveMetaData",
                Campaign.Current.SaveHandler);
            return ReflectionUtils.InvokePrivateMethod<MetaData>(
                typeof(MBSaveLoad),
                "GetSaveMetaData",
                null,
                new object[] {args});
        }
    }
}
