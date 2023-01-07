using Common;
using Common.Messaging;
using System;
using System.Collections.Generic;
using Missions.Services;
using Missions.Services.Network;
using Missions.Services.Network.Messages;

namespace Missions.Services
{
    public class CoopMission : IMission, IMissionPlayerNetwork
    {

        public IMessageBroker MessageBroker;

        public MissionClient Player;

        public List<NetworkAgent> NetworkAgents;

        public event Action<MissionCreatedInfo> MissionCreatedEvent;
        public event Action<PlayerDisconnectedInfo> PlayerDisconnectedEvent;
        public event Action<PlayerLeftInfo> PlayerLeftMissionEvent;
        public event Action<MissionResultInfo> MissionConcludedEvent;

        public void ClaimControl()
        {
            throw new NotImplementedException();
        }

        public void DetectTimeout()
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public JoinResponse JoinMission(Player player, JoinInfo joinInfo)
        {
            throw new NotImplementedException();
        }

        public void LeaveBattle()
        {
            throw new NotImplementedException();
        }

        public LeftResponse LeaveMission(Player player, PlayerLeftInfo leaveInfo)
        {
            throw new NotImplementedException();
        }

        public void RecieveConnect(MessagePayload<ConnectMessage> message)
        {
            throw new NotImplementedException();
        }

        public void RecieveControlClaim(MessagePayload<ClaimControlMessage> message)
        {
            throw new NotImplementedException();
        }

        public void RecieveDisconnect(MessagePayload<DisconnectMessage> message)
        {
            throw new NotImplementedException();
        }

        public void RecieveNumberControlled(MessagePayload<AgentControlledAmountMessage> message)
        {
            throw new NotImplementedException();
        }

        public void SendNumberControlled()
        {
            throw new NotImplementedException();
        }

        public StartNewResponse StartNewMission(Player player, MissionInfo missionInfo)
        {
            throw new NotImplementedException();
        }
    }
}
