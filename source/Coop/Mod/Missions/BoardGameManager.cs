using Common;
using Coop.Mod.Missions.Messages.BoardGames;
using Coop.Mod.Missions.Network;
using Coop.Mod.Missions.Packets.Agents;
using Coop.Mod.Patch.Agents;
using Coop.Mod.Patch.BoardGames;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using NLog;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    public class BoardGameManager
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        public NetworkMessageBroker MessageBroker { get; private set; }

        private BoardGameLogic BoardGameLogic;

        public BoardGameManager(NetworkMessageBroker messageBroker)
        {
            MessageBroker = messageBroker;

            AgentInteractionPatch.OnAgentInteraction += Handle_OnAgentInteraction;

            MessageBroker.Subscribe<BoardGameChallengeRequest>(Handle_ChallengeRequest);

        }

        private void Handle_OnAgentInteraction(Agent sender, Agent other)
        {
            if(Mission.Current.HasMissionBehavior<MissionBoardGameLogic>() &&
               Agent.Main == sender)
            {
                InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Challenge", "Pussy out",
                new Action(() => { SendGameRequest(sender, other); }), new Action(() => { })));
            }
            
        }

        private void SendGameRequest(Agent sender, Agent other)
        {
            if (NetworkAgentRegistry.AgentToId.TryGetValue(sender, out Guid senderGuid) && 
                NetworkAgentRegistry.AgentToId.TryGetValue(other, out Guid otherGuid))
            {
                BoardGameChallengeRequest request = new BoardGameChallengeRequest(senderGuid, otherGuid);
                MessageBroker.Subscribe<BoardGameChallengeResponse>(Handle_ChallengeResponse);
                MessageBroker.Publish(request);
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

            if(BoardGameLogic.IsPlayingOtherPlayer == false)
            {
                InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Accept", "Pussy out",
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
            MessageBroker.Publish(response, netPeer);

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
            MessageBroker.Publish(response, netPeer);
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
            BoardGameLogic = new BoardGameLogic(MessageBroker, gameId);
            BoardGameLogic.StartGame(startFirst, opposingAgent);
        }
    }
}
