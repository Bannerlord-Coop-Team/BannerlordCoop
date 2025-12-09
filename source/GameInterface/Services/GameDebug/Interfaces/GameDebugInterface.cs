using SandBox;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem;
using Common;
using Common.Util;
using System.Reflection;
using TaleWorlds.Library;
using Serilog;
using Common.Logging;
using GameInterface.Services.GameDebug.Patches;
using TaleWorlds.CampaignSystem;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal interface IDebugGameInterface : IGameAbstraction
    {
        void LoadDebugGame();
        void ShowAllParties();
        void LoadGame(string saveName);
    }

    internal class DebugGameInterface : IDebugGameInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<DebugGameInterface>();

        public static readonly string LOAD_GAME = "MP";

        public void LoadDebugGame()
        {
            GameLoopRunner.RunOnMainThread(InternalLoadDebugGame, false);
        }

        private static PlatformDirectoryPath SaveDir => FileDriver.SavePath;
        private static PlatformFilePath SavePath => new PlatformFilePath(SaveDir, $"{LOAD_GAME}.sav");
        private static string FullSavePath => TaleWorlds.Library.Common.PlatformFileHelper?.GetFileFullPath(SavePath);
        private void InternalLoadDebugGame()
        {
            //Logger.Information("Downloading save file to: {savePath}", FullSavePath);

            //WebDownloader webDownloader = new WebDownloader();
            //// TODO maybe uncomment for debugging
            //webDownloader.DownloadFile("https://coop.theodor.dev/MP.sav", FullSavePath);

            //Logger.Information("Downloaded save file.");

            var mp_save = MBSaveLoad.GetSaveFiles(null).FirstOrDefault(x => x.Name == LOAD_GAME);
            if (mp_save == null)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Sauvegarde '{LOAD_GAME}' introuvable"));
                return;
            }
            InformationManager.DisplayMessage(new InformationMessage($"Chargement de '{LOAD_GAME}'..."));
            Logger.Information("Loading debug save {SaveName}", LOAD_GAME);
            SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
        }

        private void StartGame(LoadResult loadResult)
        {
            InformationManager.DisplayMessage(new InformationMessage("Sauvegarde chargée, initialisation de la campagne"));
            Logger.Information("Starting game from loaded save");
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
            MouseManager.ShowCursor(false);
        }

        public void ShowAllParties()
        {
            PartyVisibilityPatch.AllPartiesVisible = true;

            GameLoopRunner.RunOnMainThread(() =>
            {
                foreach (var party in Campaign.Current.MobileParties)
                {
                    party.IsVisible = true;
                    party.Party.SetVisualAsDirty();
                }
            });
        }

        public void LoadGame(string saveName)
        {
            Logger.Information("Loading save {SaveName}", saveName);
            var mp_save = MBSaveLoad.GetSaveFiles(null).FirstOrDefault(x => x.Name == saveName);
            if (mp_save == null)
            {
                Logger.Warning("Save {SaveName} not found", saveName);
                InformationManager.DisplayMessage(new InformationMessage($"Sauvegarde '{saveName}' introuvable"));
                return;
            }
            InformationManager.DisplayMessage(new InformationMessage($"Chargement de '{saveName}'..."));
            Logger.Information("Starting save load for {SaveName}", saveName);
            SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
        }
    }
}
