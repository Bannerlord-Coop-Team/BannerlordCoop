using Network.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.CoopBattle
{
    public class MissionInfos
    {
        public struct MissionInfo
        {
            public string missionLocation;
        }

        public struct JoinInfo
        {
            public CharacterObject controlledCharacter;
            public CharacterObject[] controlledAgents;
        }

        public struct MissionCreatedInfo
        {
            public string missionLocation;
            public CharacterObject[] controlledAgents;
        }

        public struct PlayerDisconnectedInfo
        {
            public Player player;
            public EDisconnectReason reason;
        }

        public struct PlayerLeftInfo
        {
            public (Type, int)[] troopCasualties;
            public (Type, int)[] troopExperience;
            public (Type, int)[] playerExperience;
            public int playerHealth;
        }

        public struct MissionResultInfo
        {

        }

    }
}
