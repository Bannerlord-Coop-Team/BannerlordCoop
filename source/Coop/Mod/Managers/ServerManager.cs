using System.Collections.Generic;
using Coop.Mod.CLI;
using SandBox;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod.Managers
{
    public class ServerGameManager : CampaignGameManager
    {
        public ServerGameManager() : base() { }
        public ServerGameManager(LoadResult saveGameData) : base(saveGameData) { }

        ~ServerGameManager()
        {
            // TODO save all heros
        }
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            CLICommands.StartServer(new List<string>());
        }
    }
}
