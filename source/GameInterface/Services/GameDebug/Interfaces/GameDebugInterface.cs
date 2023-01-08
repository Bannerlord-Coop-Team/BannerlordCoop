using SandBox;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal class GameDebugInterface : IGameDebugInterface
    {
        public static readonly string LOAD_GAME = "MP";

        public void LoadDebugGame()
        {
            SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles(null);
            SaveGameFileInfo mp_save = saveFiles.Single(x => x.Name == LOAD_GAME);
            SandBoxSaveHelper.TryLoadSave(mp_save, StartGame, null);
        }

        private void StartGame(LoadResult loadResult)
        {
            MBGameManager.StartNewGame(new SandBoxGameManager(loadResult));
            MouseManager.ShowCursor(false);
        }
    }


}
