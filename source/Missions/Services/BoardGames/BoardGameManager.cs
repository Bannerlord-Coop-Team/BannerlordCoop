using Common;
using LiteNetLib;
using SandBox.BoardGames.MissionLogics;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem.Conversation;
using Common.Messaging;
using Missions.Services.BoardGames;
using Missions.Services.Network;
using Missions.Services.Network.Messages.Agents;
using Missions.Services.Network.Messages.BoardGames;
using Missions.Services.Network.Packets.Agents;

namespace Missions.Services.BoardGames
{
    public class BoardGameManager
    {
        private readonly ILogger m_Logger = LogManager.GetLogger<BoardGameManager>();

        private readonly LiteNetP2PClient _P2PClient;
        
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private BoardGameLogic BoardGameLogic;

        public BoardGameManager(LiteNetP2PClient P2PClient, IMessageBroker messageBroker, INetworkAgentRegistry agentRegistry)
        {
            _P2PClient = P2PClient;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;

            _messageBroker.Subscribe<AgentInteraction>(Handle_OnAgentInteraction);

            _messageBroker.Subscribe<BoardGameChallengeRequest>(Handle_ChallengeRequest);
        }

        private void Handle_OnAgentInteraction(MessagePayload<AgentInteraction> payload)
        {
            if (Mission.Current.HasMissionBehavior<MissionBoardGameLogic>() && Agent.Main == payload.What.reqAgent)
            {
                SendGameRequest(payload.What.reqAgent, payload.What.tarAgent);
            }

        }

        private void SendGameRequest(Agent sender, Agent other)
        {
            if (_agentRegistry.AgentToId.TryGetValue(sender, out Guid senderGuid) &&
                _agentRegistry.AgentToId.TryGetValue(other, out Guid otherGuid))
            {
                BoardGameChallengeRequest request = new BoardGameChallengeRequest(senderGuid, otherGuid);
                _messageBroker.Subscribe<BoardGameChallengeResponse>(Handle_ChallengeResponse);
                _P2PClient.SendAllEvent(request);
            }
            else
            {
                m_Logger.Warning("SendGameRequest failed to send");
            }
        }

        private void Handle_ChallengeRequest(MessagePayload<BoardGameChallengeRequest> payload)
        {
            Guid sender = payload.What.TargetPlayer;
            Guid other = payload.What.RequestingPlayer;
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            if (BoardGameLogic.IsPlayingOtherPlayer == false)
            {
                InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", _agentRegistry.ControlledAgents[sender].Name + " has challenged you to a board game", true, true, "Accept", "Decline",
                new Action(() => { AcceptGameRequest(sender, other, netPeer); }), new Action(() => { DenyGameRequest(sender, other, netPeer); })));
            }
            else
            {
                DenyGameRequest(sender, other, netPeer);
            }
        }

        private void AcceptGameRequest(Guid sender, Guid other, NetPeer netPeer)
        {
            Guid gameId = Guid.NewGuid();

            BoardGameChallengeResponse response = new BoardGameChallengeResponse(sender, other, true, gameId);
            _P2PClient.SendEvent(response, netPeer);

            //Has to do same thing as if (accepted) in Handle_ChallengeResponse
            if (_agentRegistry.OtherAgents.TryGetValue(netPeer, out AgentGroupController group) &&
                group.ControlledAgents.TryGetValue(other, out Agent opponent))
            {
                StartGame(false, gameId, opponent);
            }
        }

        private void DenyGameRequest(Guid sender, Guid other, NetPeer netPeer)
        {
            BoardGameChallengeResponse response = new BoardGameChallengeResponse(sender, other, false, Guid.Empty);
            _P2PClient.SendEvent(response, netPeer);
        }

        private void Handle_ChallengeResponse(MessagePayload<BoardGameChallengeResponse> payload)
        {
            bool accepted = payload.What.Accepted;
            Guid gameId = payload.What.GameId;
            Guid opponentId = payload.What.RequestingPlayer;

            if (accepted)
            {
                NetPeer netPeer = payload.Who as NetPeer;
                if (_agentRegistry.OtherAgents.TryGetValue(netPeer, out AgentGroupController group) &&
                    group.ControlledAgents.TryGetValue(opponentId, out Agent opponent))
                {
                    StartGame(true, gameId, opponent);
                }
            }
        }

        private void StartGame(bool startFirst, Guid gameId, Agent opposingAgent)
        {
            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
            CultureObject.BoardGameType gameType = Settlement.CurrentSettlement.Culture.BoardGame;
            BoardGameLogic = new BoardGameLogic(_P2PClient, _messageBroker, gameId, boardGameLogic, gameType);
            BoardGameLogic.StartGame(startFirst, opposingAgent);
        }
    }
}
