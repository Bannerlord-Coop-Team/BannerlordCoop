using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Agents.Patches;
using Missions.Services.Arena;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services
{
    /// <summary>
    /// Mission Controller that does all the logic in the Coop Arena
    /// </summary>
    internal class CoopArenaController : MissionBehavior
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly IMessageBroker _messageBroker;
        private readonly INetworkMessageBroker _networkMessageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;
        private readonly IRandomEquipmentGenerator _equipmentGenerator;

        private Agent _tempAi;
        private List<MatrixFrame> spawnFrames = new List<MatrixFrame>();
        private readonly CharacterObject[] _gameCharacters;

        public CoopArenaController(
            IMessageBroker messageBroker,
            INetworkMessageBroker networkMessageBroker,
            INetworkAgentRegistry agentRegistry, 
            IRandomEquipmentGenerator equipmentGenerator)
        {
            _messageBroker = messageBroker;
            _networkMessageBroker = networkMessageBroker;
            _agentRegistry = agentRegistry;
            _equipmentGenerator = equipmentGenerator;
            _gameCharacters = CharacterObject.All.Where(x => !x.IsHero && x.Age > 18).ToArray();
            messageBroker.Subscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            messageBroker.Subscribe<AgentDamageData>(Handle_AgentDamage);
            _networkMessageBroker.Subscribe<AgentShoot>(Handle_AgentShoot);
            _networkMessageBroker.Subscribe<AgentDied>(Handler_AgentDeath);
        }

        

        ~CoopArenaController()
        {
            _messageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            _networkMessageBroker.Unsubscribe<AgentShoot>(Handle_AgentShoot);
        }

        public override void AfterStart()
        {
            AddPlayerToArena();
        }


        /// <summary>
        /// A network damage handler for an agent
        /// </summary>
        /// <param name="payload">AgentDamage Data which include Attacker GUID, Defender GUID, Blow and AttackCollisionData</param>
        private void Handle_AgentDamage(MessagePayload<AgentDamageData> payload)
        {
            AgentDamageData agentDamaData = payload.What;
            NetPeer netPeer = payload.Who as NetPeer;
            

            Agent effectedAgent = null;
            Agent effectorAgent = null;
            // grab the network registry group controller
            _agentRegistry.OtherAgents.TryGetValue(netPeer, out AgentGroupController agentGroupController);

            // start with the attack receiver
            // first check if the receiver of the damage is one the sender's agents
            if(agentGroupController != null && agentGroupController.ControlledAgents.ContainsKey(agentDamaData.VictimAgentId)) {
                agentGroupController.ControlledAgents.TryGetValue(agentDamaData.VictimAgentId, out effectedAgent);
            }
            // otherwise next, check if it is one of our agents
            else if (_agentRegistry.ControlledAgents.ContainsKey(agentDamaData.VictimAgentId))
            {
                _agentRegistry.ControlledAgents.TryGetValue(agentDamaData.VictimAgentId, out effectedAgent);
            }
            // now with the attacker
            // check if the attacker is one of the senders (should always be true?)
            if (agentGroupController != null && agentGroupController.ControlledAgents.ContainsKey(agentDamaData.AttackerAgentId))
            {
                agentGroupController.ControlledAgents.TryGetValue(agentDamaData.AttackerAgentId, out effectorAgent);
            }
            else if (_agentRegistry.ControlledAgents.ContainsKey(agentDamaData.AttackerAgentId))
            {
                _agentRegistry.ControlledAgents.TryGetValue(agentDamaData.AttackerAgentId, out effectorAgent);
            }

            // extract the blow
            Blow b = agentDamaData.Blow;

            // assign the blow owner from our own index
            b.OwnerId = effectorAgent.Index;

            // extract the collision data
            AttackCollisionData collisionData = agentDamaData.AttackCollisionData;

            GameLoopRunner.RunOnMainThread(() =>
            {
                // register a blow on the effected agent
                effectedAgent?.RegisterBlow(b, collisionData);
            });            
        }

        private void Handle_JoinInfo(MessagePayload<NetworkMissionJoinInfo> payload)
        {
            Logger.Debug("Received join request");
            NetPeer netPeer = (NetPeer)payload.Who;

            NetworkMissionJoinInfo joinInfo = payload.What;

            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer}",
                joinInfo.CharacterObject.IsPlayerCharacter ? "Player" : "Agent",
                joinInfo.CharacterObject.Name, newAgentId, netPeer.EndPoint);

            Agent newAgent = SpawnAgent(startingPos, joinInfo.CharacterObject, true);
            _agentRegistry.RegisterNetworkControlledAgent(netPeer, joinInfo.PlayerId, newAgent);


            for (int i = 0; i < joinInfo.UnitIdString?.Length; i++)
            {
                Agent tempAi = SpawnAgent(joinInfo.UnitStartingPosition[i], CharacterObject.Find(joinInfo.UnitIdString[i]), true);

                _agentRegistry.RegisterNetworkControlledAgent(netPeer, joinInfo.UnitId[i], tempAi);
            }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            if (Agent.Main.Team.TeamAgents.Contains(shooterAgent))
            {
                _agentRegistry.TryGetAgentId(shooterAgent, out Guid shooterAgentGuid);
                AgentShoot message = new AgentShoot(shooterAgentGuid, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

                _networkMessageBroker.PublishNetworkEvent(message);
            }
        }

        private static MethodInfo OnAgentShootMissileMethod = typeof(Mission).GetMethod("OnAgentShootMissile", BindingFlags.NonPublic | BindingFlags.Instance);
        private void Handle_AgentShoot(MessagePayload<AgentShoot> payload)
        {
            _agentRegistry.TryGetGroupController(payload.Who as NetPeer, out AgentGroupController agentGroupController);

            AgentShoot shot = payload.What;
            OnAgentShootMissileMethod.Invoke(Mission.Current, new object[] { 
                agentGroupController.ControlledAgents[shot.AgentGuid], 
                shot.WeaponIndex, 
                shot.Position,
                shot.Velocity, 
                shot.Orientation, 
                shot.HasRigidBody, 
                true, 
                shot.ForcedMissileIndex });
        }

        private void Handler_AgentDeath(MessagePayload<AgentDied> obj)
        {
            Agent agent = obj.What.Agent;
            if(_agentRegistry.TryGetAgentId(agent, out Guid agentId))
            {
                _agentRegistry.RemoveControlledAgent(agentId);
            }
        }

        public void AddPlayerToArena()
        {
            // reset teams if any exists
            Mission.Current.ResetMission();

            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

            // players is attacker team
            Mission.Current.PlayerTeam = Mission.Current.AttackerTeam;

            spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }

            // get a random spawn point
            Random rand = new Random();
            MatrixFrame randomElement = spawnFrames[rand.Next(spawnFrames.Count)];  


            // spawn an instance of the player (controlled by default)
            Agent player = SpawnPlayerAgent(CharacterObject.PlayerCharacter, randomElement);

            Agent.Main.SetTeam(Mission.Current.PlayerTeam, false);

            Agent ai = SpawnAgent(randomElement.origin, _gameCharacters[rand.Next(_gameCharacters.Length - 1)], false);

            _agentRegistry.RegisterControlledAgent(Guid.NewGuid(), player);
            _agentRegistry.RegisterControlledAgent(Guid.NewGuid(), ai);
        }


        private static readonly PropertyInfo Hero_BattleEquipment = typeof(Hero).GetProperty("BattleEquipment", BindingFlags.Public | BindingFlags.Instance);
        /// <summary>
        /// Spawn an agent based on its character object and frame. For now, Main agent character object is used
        /// This should be the real character object in the future
        /// </summary>
        private Agent SpawnPlayerAgent(CharacterObject character, MatrixFrame frame)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData = agentBuildData.Team(Mission.Current.PlayerTeam).InitialPosition(frame.origin);
            agentBuildData.NoHorses(true);

            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Equipment generatedEquipment = _equipmentGenerator.CreateRandomEquipment(true);
            agentBuildData.Equipment(generatedEquipment);
            Hero_BattleEquipment.SetValue(character.HeroObject, generatedEquipment); 
            agentBuildData.InitialDirection(vec);
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
            agentBuildData.Controller(Agent.ControllerType.Player);

            Agent agent = default;
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            }, true);
            agent.FadeIn();

            return agent;
        }

        public Agent SpawnAgent(Vec3 startingPos, CharacterObject character, bool isEnemy)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData.InitialPosition(startingPos);
            agentBuildData.Team(isEnemy ? Mission.Current.PlayerEnemyTeam : Mission.Current.PlayerTeam);
            agentBuildData.InitialDirection(Vec2.Forward);
            agentBuildData.NoHorses(true);
            agentBuildData.Equipment(character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment);
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
            agentBuildData.Controller(isEnemy ? Agent.ControllerType.None : Agent.ControllerType.AI);

            Agent agent = default;
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            }, true);

            if (agent.IsAIControlled)
            {
                agent.SetWatchState(Agent.WatchState.Alarmed);
            }

            return agent;
        }

        // DEBUG METHOD: Starts an Arena fight
        public void StartArenaFight()
        {
            // reset teams if any exists
            Mission.Current.ResetMission();

            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

            // players is defender team
            Mission.Current.PlayerTeam = Mission.Current.DefenderTeam;


            // find areas of spawn
            List<MatrixFrame> spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }
            // get a random spawn point
            MatrixFrame randomElement = spawnFrames.GetRandomElement();


            // spawn an instance of the player (controlled by default)
            SpawnPlayerAgent(CharacterObject.PlayerCharacter, randomElement);
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            _agentRegistry.Clear();
        }
    }
}