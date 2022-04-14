using System.Collections.Generic;
using Coop.Mod.Patch;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Logger = NLog.Logger;

namespace Coop.Mod
{
    public static class SaveLoad
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static SaveGameData SaveGame(Game game, ISaveDriver driver)
        {
            EntitySystem<GameHandler> entitySystem =
                Utils.GetPrivateField<EntitySystem<GameHandler>>(
                    typeof(Game),
                    "_gameEntitySystem",
                    game);
            MetaData metaData = GetMetaData();

            foreach (GameHandler gameHandler in entitySystem.Components)
            {
                gameHandler.OnBeforeSave();
            }

            SaveOutput saveOutput = SaveManager.Save(game, metaData, Main.LOAD_GAME, driver);
            saveOutput.PrintStatus();
            foreach (GameHandler gameHandler2 in entitySystem.Components)
            {
                gameHandler2.OnAfterSave();
            }

            return new SaveGameData(metaData, saveOutput);
        }

        public static void LoadGame(LoadResult loadResult)
        {
            if (Game.Current != null)
            {
                GameStateManager.Current.CleanStates();
                GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
            }

            //MBGameManager.StartNewGame(new ClientManager(loadResult));
        }

        public static LoadResult LoadSaveGameData(ISaveDriver driver)
        {
            List<ModuleInfo> currentModules = GetModules();
            LoadResult loadResult = SaveManager.Load(Main.LOAD_GAME, driver, true);
            if (loadResult.Successful)
            {
                return loadResult;
            }

            Logger.Error("{loadResult}", loadResult.ToFriendlyString());

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

        private static List<ModuleInfo> GetModules()
        {
            return Utils.InvokePrivateMethod<List<ModuleInfo>>(
                typeof(ModuleHelper),
                "GetPhysicalModules",
                null,
                new object[] {});
        }

        private static MetaData GetMetaData()
        {
            CampaignSaveMetaDataArgs args = Utils.InvokePrivateMethod<CampaignSaveMetaDataArgs>(
                typeof(SaveHandler),
                "GetSaveMetaData",
                Campaign.Current.SaveHandler);
            return Utils.InvokePrivateMethod<MetaData>(
                typeof(MBSaveLoad),
                "GetSaveMetaData",
                null,
                new object[] {args});
        }
    }
}
