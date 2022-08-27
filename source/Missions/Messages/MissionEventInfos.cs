using Missions.Network;
using System;
using TaleWorlds.CampaignSystem;

namespace Missions.Messages
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
