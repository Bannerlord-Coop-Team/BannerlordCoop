using Common;
using Common.Logging;
using Common.Messaging;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Network.Messages;
using Missions.Services.Network.PacketHandlers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace Missions.Services.Network
{
    public class CoopMissionNetworkBehavior : MissionBehavior
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopMissionNetworkBehavior>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly LiteNetP2PClient _client;
        private readonly Guid _playerId;
        public static List<Guid> _unitId = new List<Guid>();

        private readonly TimeSpan WaitForConnectionsTime = TimeSpan.FromSeconds(1);

        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;
        private readonly MovementHandler _movementHandler;
        private readonly EventPacketHandler _eventPacketHandler;

        public CoopMissionNetworkBehavior(
            LiteNetP2PClient client, 
            IMessageBroker messageBroker,
            INetworkAgentRegistry agentRegistry)
        {
            _client = client;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;
            _playerId = Guid.NewGuid();

            _movementHandler = new MovementHandler(_client, _messageBroker, _agentRegistry);
            _eventPacketHandler = new EventPacketHandler(_client, _messageBroker);

            _messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);

            _client.AddHandler(_eventPacketHandler);
        }

        public override void AfterStart()
        {
            string sceneName = Mission.SceneName;
            _client.NatPunch(sceneName);

            //// TODO find way to make this not a task
            //Task.Factory.StartNew(async () =>
            //{
            //    while (Mission == null || Mission.IsLoadingFinished == false)
            //    {
            //        await Task.Delay(100);
            //    }

            //    string sceneName = Mission.SceneName;
            //    _client.NatPunch(sceneName);

            //    await Task.Delay(WaitForConnectionsTime);
            //});
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

            List<Vec3> unitPositions = new List<Vec3>();
            List<string> unitIdStrings = new List<string>();
            foreach (Agent agent in Agent.Main.Team.TeamAgents)
            {
                if (agent != Agent.Main)
                {
                    unitPositions.Add(agent.Position);
                    unitIdStrings.Add(agent.Character.StringId);
                }
            }

            MissionJoinInfo request = new MissionJoinInfo(characterObject, _playerId, Agent.Main.Position, _unitId.ToArray(), unitPositions.ToArray(), unitIdStrings.ToArray());
            _client.SendEvent(request, peer);
            Logger.Information("Sent {AgentType} Join Request for {AgentName}({PlayerID}) to {Peer}",
                characterObject.IsPlayerCharacter ? "Player" : "Agent",
                characterObject.Name, request.PlayerId, peer.EndPoint);
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();

            _messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);

            _client.RemoveHandler(_eventPacketHandler);
            _client.Stop();
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            _messageBroker.Publish(this, new AgentDeleted(affectedAgent));
            

            base.OnAgentDeleted(affectedAgent);
        }

        protected override void OnEndMission()
        {
            _client.Dispose();
            MBGameManager.EndGame();
            base.OnEndMission();
        }
    }
}
