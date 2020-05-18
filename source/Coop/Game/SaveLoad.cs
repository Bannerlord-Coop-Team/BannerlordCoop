using System.Collections.Generic;
using Coop.Game.Managers;
using Coop.Game.Patch;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Game
{
    public static class SaveLoad
    {
        public static SaveOutput SaveGame(TaleWorlds.Core.Game game, ISaveDriver driver)
        {
            EntitySystem<GameHandler> entitySystem =
                Utils.GetPrivateField<EntitySystem<GameHandler>>(
                    typeof(TaleWorlds.Core.Game),
                    "_gameEntitySystem",
                    game);
            MetaData metaData = GetMetaData();

            // Code copied from TaleWorlds.Game.Save(MetaData, ISaveDriver)
            foreach (GameHandler gameHandler in entitySystem.Components)
            {
                gameHandler.OnBeforeSave();
            }

            SaveOutput saveOutput = SaveManager.Save(game, metaData, driver);
            saveOutput.PrintStatus();
            foreach (GameHandler gameHandler2 in entitySystem.Components)
            {
                gameHandler2.OnAfterSave();
            }
            // End code copy

            return saveOutput;
        }

        public static void LoadGame(LoadResult loadResult)
        {
            if (TaleWorlds.Core.Game.Current != null)
            {
                GameStateManager.Current.CleanStates();
                GameStateManager.Current = Module.CurrentModule.GlobalGameStateManager;
            }

            MBGameManager.StartNewGame(new ClientGameManager(loadResult));
        }

        public static LoadGameResult LoadSaveGameData(ISaveDriver driver)
        {
            List<ModuleInfo> currentModules = GetModules();
            LoadResult loadResult = SaveManager.Load(driver, true);
            if (loadResult.Successful)
            {
                return new LoadGameResult(
                    loadResult,
                    CheckModules(driver.LoadMetaData(), currentModules));
            }

            return null;
        }

        private static List<ModuleCheckResult> CheckModules(
            MetaData fileMetaData,
            List<ModuleInfo> loadedModules)
        {
            return Utils.InvokePrivateMethod<List<ModuleCheckResult>>(
                typeof(MBSaveLoad),
                "CheckModules",
                null,
                new object[] {fileMetaData, loadedModules});
        }

        private static List<ModuleInfo> GetModules()
        {
            List<ModuleInfo> list = new List<ModuleInfo>();
            foreach (string moduleName in Utilities.GetModulesNames())
            {
                ModuleInfo moduleInfo = new ModuleInfo();
                moduleInfo.Load(moduleName);
                list.Add(moduleInfo);
            }

            return list;
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
