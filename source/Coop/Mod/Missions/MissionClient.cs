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
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    public class MissionClient
    {
        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        public MovementHandler MovementHandler { get; private set; }

        public NetworkMessageBroker MessageBroker { get; private set; }

        private SerializableCharacterObject serializedCharacterObject;

        private readonly LiteNetP2PClient m_Client;

        public MissionClient(LiteNetP2PClient client)
        {
            m_Client = client;
            MessageBroker = new NetworkMessageBroker(m_Client);
            MovementHandler = new MovementHandler(m_Client);

            serializedCharacterObject = new SerializableCharacterObject(CharacterObject.PlayerCharacter, Guid.NewGuid());

            MessageBroker.Subscribe<MissionJoinRequest>(Handle_JoinRequest);
            MessageBroker.Subscribe<MissionJoinResponse>(Handle_JoinResponse);
        }

        public void SendJoinRequest()
        {
            m_Logger.Info("Sending join request");
            Guid playerId = MovementHandler.ControlledAgents.Where(kvp => kvp.Value == Agent.Main).Single().Key;
            MissionJoinRequest request = new MissionJoinRequest(playerId, Agent.Main.Position);
            MessageBroker.Publish(request);
        }

        private void Handle_JoinRequest(MessagePayload<MissionJoinRequest> payload)
        {
            m_Logger.Info("Receive join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            // TODO remove test code
            Agent newAgent = MissionTestGameManager.AddPlayerToArena(false);
            m_Logger.Info($"Creating new agent at {payload.What.OriginalPosition}");
            newAgent.TeleportToPosition(payload.What.OriginalPosition);
            MovementHandler.RegisterAgent(payload.What.PlayerId, newAgent);

            Guid playerId = MovementHandler.ControlledAgents.Where(kvp => kvp.Value == Agent.Main).Single().Key;

            MissionJoinResponse response = new MissionJoinResponse(playerId, Agent.Main.Position);
            MessageBroker.Publish(response, netPeer);
        }

        private void Handle_JoinResponse(MessagePayload<MissionJoinResponse> payload)
        {
            m_Logger.Info("Receive join response");

            // TODO remove test code
            Agent newAgent = MissionTestGameManager.AddPlayerToArena(false);
            m_Logger.Info($"Creating new agent at {payload.What.OriginalPosition}");
            MovementHandler.RegisterAgent(payload.What.PlayerId, newAgent);
        }
    }
}
