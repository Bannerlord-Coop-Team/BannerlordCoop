using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

namespace Coop.Mod.Managers
{
    class TestMPManger : MultiplayerGameManager
    {

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            base.OnGameLoaded(game, initializerObject);
            MultiplayerMissions.OpenFreeForAllMission("tutorial_training_field");
        }
    }
}
