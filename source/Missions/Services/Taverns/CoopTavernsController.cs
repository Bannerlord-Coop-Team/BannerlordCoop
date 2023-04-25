using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Missions.Services.BoardGames;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Taverns
{
    public class CoopTavernsController : MissionBehavior, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly INetworkMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private readonly BoardGameManager _boardGameManager;

        public CoopTavernsController(LiteNetP2PClient client, 
            INetworkMessageBroker messageBroker, 
            INetworkAgentRegistry agentRegistry,
            BoardGameManager boardGameManager)
        {
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;
            _boardGameManager = boardGameManager;

            messageBroker.Subscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
        }

        public void Dispose()
        {
            _messageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            Dispose();
        }

        private void Handle_JoinInfo(MessagePayload<NetworkMissionJoinInfo> payload)
        {
            Logger.Debug("Received join request");
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            NetworkMissionJoinInfo joinInfo = payload.What;

            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer}",
                joinInfo.CharacterObject.IsPlayerCharacter ? "Player" : "Agent",
                joinInfo.CharacterObject.Name, newAgentId, netPeer.EndPoint);

            
            Agent newAgent = SpawnAgent(startingPos, joinInfo.CharacterObject);
            _agentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
        }

        public Agent SpawnAgent(Vec3 startingPos, CharacterObject character)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData.InitialPosition(startingPos);
            agentBuildData.Team(Mission.Current.PlayerAllyTeam);
            agentBuildData.InitialDirection(Vec2.Forward);
            agentBuildData.NoHorses(true);
            agentBuildData.Equipment(character.FirstCivilianEquipment);
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
            agentBuildData.Controller(Agent.ControllerType.None);

            Agent agent = default;
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            }, true);

            return agent;
        }
    }
}
