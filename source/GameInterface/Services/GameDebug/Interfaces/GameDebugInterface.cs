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

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal interface IDebugGameInterface : IGameAbstraction
    {
        void LoadDebugGame();
    }

    internal class DebugGameInterface : IDebugGameInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<DebugGameInterface>();

        public static readonly string LOAD_GAME = "MP";

        public void LoadDebugGame()
        {
            GameLoopRunner.RunOnMainThread(InternalLoadDebugGame, false);
        }

        private static readonly PropertyInfo FileDriver_SavePath = typeof(FileDriver).GetProperty("SavePath", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly PlatformDirectoryPath SaveDir = (PlatformDirectoryPath)FileDriver_SavePath?.GetValue(null);
        private static readonly PlatformFilePath SavePath = new PlatformFilePath(SaveDir, $"{LOAD_GAME}.sav");
        private static readonly string FullSavePath = TaleWorlds.Library.Common.PlatformFileHelper?.GetFileFullPath(SavePath);
        private void InternalLoadDebugGame()
        {
            Logger.Information("Downloading save file to: {savePath}", FullSavePath);

            GoogleDriveDownloader saveDownloader = new GoogleDriveDownloader();
            saveDownloader.DownloadFile("https://drive.google.com/file/d/10lZ8E7JdbOMuh29PZggsoCU_-blAs6wd/view?usp=share_link", FullSavePath);

            Logger.Information("Downloaded save file.");

            SaveGameFileInfo mp_save = MBSaveLoad.GetSaveFiles(null).Single(x => x.Name == LOAD_GAME);
            SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
        }

        private void StartGame(LoadResult loadResult)
        {
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
            MouseManager.ShowCursor(false);
        }

        private void LoadGameFromServer()
        {
            // TODO
        }
    }
}
