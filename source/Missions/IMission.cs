﻿using Missions.Messages;
using Missions.Services.Network;
using System;

namespace Missions
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
