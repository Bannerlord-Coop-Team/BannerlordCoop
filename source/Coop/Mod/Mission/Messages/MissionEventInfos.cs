using Network.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Mission.Messages
{
    public struct MissionInfo
    {
        public string MissionLocation;
    }

    public struct JoinInfo
    {
        public CharacterObject ControlledCharacter;
        public CharacterObject[] ControlledAgents;
    }

    public struct MissionCreatedInfo
    {
        public string MissionLocation;
        public CharacterObject[] ControlledAgents;
    }

    public struct PlayerDisconnectedInfo
    {
        public Player Player;
        public EDisconnectReason Reason;
    }

    public struct PlayerLeftInfo
    {
        public (Type, int)[] TroopCasualties;
        public (Type, int)[] TroopExperience;
        public (Type, int)[] PlayerExperience;
        public int PlayerHealth;
    }

    public struct MissionResultInfo
    {

    }
}
