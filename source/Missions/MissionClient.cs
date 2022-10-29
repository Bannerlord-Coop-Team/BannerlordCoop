using Common;
using LiteNetLib;
using Missions;
using Missions.Messages;
using Missions.Network;
using Missions.Packets.Agents;
using NLog;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    public class MissionClient : IDisposable
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        public BoardGameManager BoardGameManager { get; private set; }
        public MovementHandler MovementHandler { get; private set; }
        public INetworkMessageBroker MessageBroker { get; private set; }

        private readonly LiteNetP2PClient m_Client;

        private readonly Guid m_PlayerId;

        public MissionClient(LiteNetP2PClient client)
        {
            m_Client = client;
            m_PlayerId = Guid.NewGuid();
            MessageBroker = new NetworkMessageBroker(m_Client);
            BoardGameManager = new BoardGameManager(MessageBroker);
            MovementHandler = new MovementHandler(m_Client);

            m_Client.OnClientConnected += SendJoinInfo;

            MessageBroker.Subscribe<MissionJoinInfo>(Handle_JoinInfo);
        }

        ~MissionClient()
        {
            Dispose();
        }

        public void Dispose()
        {
            m_Client.OnClientConnected -= SendJoinInfo;
            MessageBroker.Unsubscribe<MissionJoinInfo>(Handle_JoinInfo);

            MovementHandler.Dispose();
            MessageBroker.Dispose();
        }

        public void SendJoinInfo(NetPeer peer)
        {
            try
            {
                m_Logger.Info("Sending join request");
                NetworkAgentRegistry.RegisterControlledAgent(m_PlayerId, Agent.Main);

                MissionJoinInfo request = new MissionJoinInfo(m_PlayerId, Agent.Main.Position);
                MessageBroker.Publish(request, peer);
            }
            catch (Exception ex)
            {
                m_Logger.Error(ex.Message);
            }
        }

        private void Handle_JoinInfo(MessagePayload<MissionJoinInfo> payload)
        {
            m_Logger.Info("Receive join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");
            Guid newAgentId = payload.What.PlayerId;
            Vec3 startingPos = payload.What.StartingPosition;

            // TODO remove test code
            Agent newAgent = MissionTestGameManager.SpawnAgent(startingPos);

            NetworkAgentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
        }
    }
}
