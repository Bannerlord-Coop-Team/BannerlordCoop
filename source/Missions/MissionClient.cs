using Common;
using LiteNetLib;
using Missions;
using Missions.Messages;
using Missions.Network;
using Missions.Packets.Agents;
using System;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    public class MissionClient : IDisposable
    {
	    public BoardGameManager BoardGameManager { get; private set; }
        public MovementHandler MovementHandler { get; private set; }
        public INetworkMessageBroker MessageBroker { get; private set; }


        private readonly ILogger m_Logger = LogManager.GetLogger<MissionClient>();
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
            m_Logger.Information("Sending join request");
            NetworkAgentRegistry.RegisterControlledAgent(m_PlayerId, Agent.Main);

            CharacterObject characterObject = CharacterObject.PlayerCharacter;
            MissionJoinInfo request = new MissionJoinInfo(characterObject, m_PlayerId, Agent.Main.Position);
            MessageBroker.Publish(request, peer);
        }

        private void Handle_JoinInfo(MessagePayload<MissionJoinInfo> payload)
        {
            m_Logger.Information("Receive join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            MissionJoinInfo joinInfo = payload.What;
            
            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            // TODO remove test code
            Agent newAgent = MissionTestGameManager.SpawnAgent(startingPos, joinInfo.CharacterObject);

            NetworkAgentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
        }
    }
}
