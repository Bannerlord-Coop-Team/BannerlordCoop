using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace Missions.Services.Network
{
    public class CoopMissionNetworkBehavior : MissionBehavior, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopMissionNetworkBehavior>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        private readonly LiteNetP2PClient _client;

        private readonly INetworkMessageBroker _networkMessageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;
        private readonly MovementHandler _movementHandler;
        private readonly EventPacketHandler _eventPacketHandler;

        public CoopMissionNetworkBehavior(
            LiteNetP2PClient client, 
            INetworkMessageBroker messageBroker,
            INetworkAgentRegistry agentRegistry,
            MovementHandler movementHandler,
            EventPacketHandler eventPacketHandler)
        {
            _client = client;
            _networkMessageBroker = messageBroker;
            _agentRegistry = agentRegistry;
            _movementHandler = movementHandler;
            _eventPacketHandler = eventPacketHandler;
        }

        public void Dispose()
        {
            _agentRegistry.Clear();
            _client.Dispose();
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            MBGameManager.EndGame();
            Dispose();
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            Dispose();
        }

        public override void OnRenderingStarted()
        {
            string sceneName = Mission.SceneName;
            _client.NatPunch(sceneName);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            _networkMessageBroker.Publish(this, new AgentDeleted(affectedAgent));
            
            base.OnAgentDeleted(affectedAgent);
        }
    }
}
