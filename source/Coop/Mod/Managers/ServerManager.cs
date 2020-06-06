using System.Collections.Generic;
using Coop.Mod.DebugUtil;
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

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            CLICommands.StartServer(new List<string>());
        }
    }
}
