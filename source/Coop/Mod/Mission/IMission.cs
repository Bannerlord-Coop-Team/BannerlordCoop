using Coop.Mod.Mission.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Mission
{
    public interface IMission
    {
        #region Events
        event Action<MissionCreatedInfo> MissionCreatedEvent;

        event Action<PlayerDisconnectedInfo> PlayerDisconnectedEvent;

        event Action<PlayerLeftInfo> PlayerLeftMissionEvent;

        event Action<MissionResultInfo> MissionConcludedEvent;
        #endregion


        StartNewResponse StartNewMission(Player player, MissionInfo missionInfo);

        JoinResponse JoinMission(Player player, JoinInfo missionInfo);

        LeftResponse LeaveMission(Player player, PlayerLeftInfo leaveInfo);
    }
}
