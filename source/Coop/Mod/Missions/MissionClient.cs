using Common;
using Common.Messaging;
using Coop.Mod.Missions.Messages;
using Coop.Mod.Missions.Network;
using Coop.Mod.Missions.Packets.Agents;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using Network.Infrastructure;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    public class MissionClient : IDisposable
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        public BoardGameManager BoardGameManager { get; private set; }
        public MovementHandler MovementHandler { get; private set; }
        public NetworkMessageBroker MessageBroker { get; private set; }

        private readonly LiteNetP2PClient m_Client;

        private readonly Guid m_PlayerId;

        public MissionClient(LiteNetP2PClient client)
        {
            m_Client = client;
            m_PlayerId = Guid.NewGuid();
            MessageBroker = new NetworkMessageBroker(m_Client);
            BoardGameManager = new BoardGameManager(MessageBroker);
            MovementHandler = new MovementHandler(m_Client);

            MessageBroker.Subscribe<MissionJoinRequest>(Handle_JoinRequest);
            MessageBroker.Subscribe<MissionJoinResponse>(Handle_JoinResponse);
        }

        public void Dispose()
        {
            MovementHandler.Dispose();
            MessageBroker.Dispose();
        }

        public void SendJoinRequest()
        {
            try
            {
                m_Logger.Info("Sending join request");
                NetworkAgentRegistry.RegisterControlledAgent(m_PlayerId, Agent.Main);
                MissionJoinRequest request = new MissionJoinRequest(m_PlayerId, Agent.Main.Position);
                MessageBroker.Publish(request);

                // If first client remove response handling
                // When this doesn't happen after the 3rd client joins
                // There will be a double add on the 1st client
                if(m_Client.ConnectedPeersCount < 1)
                {
                    MessageBroker.Unsubscribe<MissionJoinResponse>(Handle_JoinResponse);
                }
            }
            catch (Exception ex)
            {
                m_Logger.Error(ex.Message);
            }
        }

        private void Handle_JoinRequest(MessagePayload<MissionJoinRequest> payload)
        {
            m_Logger.Info("Receive join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");
            Guid newAgentId = payload.What.PlayerId;
            Vec3 startingPos = payload.What.StartingPosition;

            // TODO remove test code
            Agent newAgent = MissionTestGameManager.SpawnAgent(startingPos);
            
            NetworkAgentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);

            MissionJoinResponse response = new MissionJoinResponse(m_PlayerId, Agent.Main.Position);
            MessageBroker.Publish(response, netPeer);
        }

        private void Handle_JoinResponse(MessagePayload<MissionJoinResponse> payload)
        {
            m_Logger.Info("Receive join response");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            Guid playerId = payload.What.PlayerId;
            Vec3 startingPos = payload.What.StartingPosition;

            // TODO remove test code
            Agent newAgent = MissionTestGameManager.SpawnAgent(startingPos);
            m_Logger.Info($"Creating new agent at {startingPos}");
            NetworkAgentRegistry.RegisterNetworkControlledAgent(netPeer, playerId, newAgent);

            // We no longer need to listen for responses
            MessageBroker.Unsubscribe<MissionJoinResponse>(Handle_JoinResponse);
        }
    }
}
