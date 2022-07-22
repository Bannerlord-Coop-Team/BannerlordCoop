using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.CoopBattle
{
    public interface IMissionInterface
    {

        MissionMessages.StartNewResponse StartNewMission(Player player, MissionInfos.MissionInfo missionInfo);

        MissionMessages.JoinResponse JoinMission(Player player, MissionInfos.JoinInfo missionInfo);

        MissionMessages.LeftResponse LeaveMission(Player player, MissionInfos.PlayerLeftInfo leaveInfo);


        public delegate MissionInfos.MissionCreatedInfo MissionCreated(MissionInfos.MissionCreatedInfo missionCreatedInfo);

        event MissionCreated MissionCreatedEvent;

        public delegate MissionInfos.PlayerDisconnectedInfo PlayerDisonnected(MissionInfos.PlayerDisconnectedInfo playerDisconnectedInfo);

        event PlayerDisonnected PlayerDisconnectedEvent;

        public delegate MissionInfos.PlayerLeftInfo PlayerLeftMission(MissionInfos.PlayerLeftInfo playerLeftInfo);

        event PlayerLeftMission PlayerLeftMissionEvent;

        public delegate MissionInfos.MissionResultInfo MissionConcluded(MissionInfos.MissionResultInfo missionResultInfo);

        event MissionConcluded MissionConcludedEvent;




    }
}
