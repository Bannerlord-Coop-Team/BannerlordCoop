using Common;
using Coop.Mod.Missions;
using Coop.Mod.Patch.Agents;
using LiteNetLib;
using Missions.Messages.BoardGames;
using Missions.Network;
using Missions.Packets.Agents;
using NLog;
using SandBox.BoardGames.MissionLogics;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Conversation;
using Common.Messaging;

namespace Missions
{
    public class BoardGameManager
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        private readonly LiteNetP2PClient _P2PClient;
        
        private readonly IMessageBroker _messageBroker;

        private BoardGameLogic BoardGameLogic;

        public BoardGameManager(LiteNetP2PClient P2PClient, IMessageBroker messageBroker)
        {
            _P2PClient = P2PClient;
            _messageBroker = messageBroker;

            ProcessSentencePatch.OnAgentInteraction += Handle_OnAgentInteraction;

            _messageBroker.Subscribe<BoardGameChallengeRequest>(Handle_ChallengeRequest);
        }

        private void Handle_OnAgentInteraction(Agent sender, Agent other)
        {
            if (Mission.Current.HasMissionBehavior<MissionBoardGameLogic>() && Agent.Main == sender)
            {
                SendGameRequest(sender, other);
            }

        }

        private void SendGameRequest(Agent sender, Agent other)
        {
            if (NetworkAgentRegistry.AgentToId.TryGetValue(sender, out Guid senderGuid) &&
                NetworkAgentRegistry.AgentToId.TryGetValue(other, out Guid otherGuid))
            {
                BoardGameChallengeRequest request = new BoardGameChallengeRequest(senderGuid, otherGuid);
                _messageBroker.Subscribe<BoardGameChallengeResponse>(Handle_ChallengeResponse);
                _messageBroker.Publish(request);
            }
            else
            {
                m_Logger.Warn("SendGameRequest failed to send");
            }
        }

        private void Handle_ChallengeRequest(MessagePayload<BoardGameChallengeRequest> payload)
        {
            Guid sender = payload.What.TargetPlayer;
            Guid other = payload.What.RequestingPlayer;
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            if (BoardGameLogic.IsPlayingOtherPlayer == false)
            {
                InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", NetworkAgentRegistry.ControlledAgents[sender].Name + " has challenged you to a board game", true, true, "Accept", "Decline",
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
            _messageBroker.Publish(response, netPeer);

            //Has to do same thing as if (accepted) in Handle_ChallengeResponse
            if (NetworkAgentRegistry.OtherAgents.TryGetValue(netPeer, out AgentGroupController group) &&
                group.ControlledAgents.TryGetValue(other, out Agent opponent))
            {
                StartGame(false, gameId, opponent);
            }
        }

        private void DenyGameRequest(Guid sender, Guid other, NetPeer netPeer)
        {
            BoardGameChallengeResponse response = new BoardGameChallengeResponse(sender, other, false, Guid.Empty);
            _messageBroker.Publish(response, netPeer);
        }

        private void Handle_ChallengeResponse(MessagePayload<BoardGameChallengeResponse> payload)
        {
            bool accepted = payload.What.Accepted;
            Guid gameId = payload.What.GameId;
            Guid opponentId = payload.What.RequestingPlayer;

            if (accepted)
            {
                NetPeer netPeer = payload.Who as NetPeer;
                if (NetworkAgentRegistry.OtherAgents.TryGetValue(netPeer, out AgentGroupController group) &&
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
