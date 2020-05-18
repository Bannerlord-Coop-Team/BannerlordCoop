using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MBMultiplayerCampaign.Serializers;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TGame = TaleWorlds.Core.Game;

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
    }
}
