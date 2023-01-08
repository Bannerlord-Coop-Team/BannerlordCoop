using Common;
using Common.Messaging;
using LiteNetLib;
using System;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Missions.Services.BoardGames;
using Missions.Services.Network.PacketHandlers;
using Missions.Messages;
using Missions.Services.Network.Messages;
using Missions.Services.Agents.Packets;

namespace Missions.Services.Network
{
    public class MissionClient : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MissionClient>();

        public BoardGameManager BoardGameManager { get; private set; }
        public MovementHandler MovementHandler { get; private set; }
        private readonly EventPacketHandler _eventPacketHandler;
        private readonly INetworkAgentRegistry _agentRegistry;
        private readonly IMessageBroker _messageBroker;
        private readonly LiteNetP2PClient _client;
        private readonly Guid _playerId;

        public MissionClient(LiteNetP2PClient client, IMessageBroker messageBroker)
        {
            _client = client;
            _playerId = Guid.NewGuid();
            _messageBroker = messageBroker;
            _agentRegistry = NetworkAgentRegistry.Instance;
            BoardGameManager = new BoardGameManager(client, _messageBroker, _agentRegistry);
            MovementHandler = new MovementHandler(_client, _messageBroker, _agentRegistry);
            _eventPacketHandler = new EventPacketHandler(_client, _messageBroker);

            _messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);
            _messageBroker.Subscribe<MissionJoinInfo>(Handle_JoinInfo);

            _client.AddHandler(_eventPacketHandler);
        }



        ~MissionClient()
        {
            Dispose();
        }

        public void Dispose()
        {
            _client.RemoveHandler(_eventPacketHandler);

            _messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
            _messageBroker.Unsubscribe<MissionJoinInfo>(Handle_JoinInfo);

            MovementHandler.Dispose();
            _messageBroker.Dispose();

            _agentRegistry.Clear();
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> payload)
        {
            SendJoinInfo(payload.What.Peer);
        }

        private void SendJoinInfo(NetPeer peer)
        {
            Logger.Debug("Sending join request");
            _agentRegistry.RegisterControlledAgent(_playerId, Agent.Main);

            CharacterObject characterObject = CharacterObject.PlayerCharacter;
            MissionJoinInfo request = new MissionJoinInfo(characterObject, _playerId, Agent.Main.Position);
            _client.SendEvent(request, peer);
            Logger.Information("Sent {AgentType} Join Request for {AgentName}({PlayerID}) to {Peer}",
                characterObject.IsPlayerCharacter ? "Player" : "Agent",
                characterObject.Name, request.PlayerId, peer.EndPoint);
        }

        private void Handle_JoinInfo(MessagePayload<MissionJoinInfo> payload)
        {
            Logger.Debug("Received join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            MissionJoinInfo joinInfo = payload.What;

            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer}",
                joinInfo.CharacterObject.IsPlayerCharacter ? "Player" : "Agent",
                joinInfo.CharacterObject.Name, newAgentId, netPeer.EndPoint);
            // TODO remove test code
            Agent newAgent = MissionTestGameManager.SpawnAgent(startingPos, joinInfo.CharacterObject);
            _agentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
        }
    }
}
