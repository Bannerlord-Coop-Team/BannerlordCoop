using Missions.Messages;
using Missions.Services.Network;
using System;

namespace Missions
{
    public interface IMission
    {
        StartNewResponse StartNewMission(Player player, MissionInfo missionInfo);

        JoinResponse JoinMission(Player player, JoinInfo missionInfo);

        LeftResponse LeaveMission(Player player, PlayerLeftInfo leaveInfo);
    }
}
