using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Serialization;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Agents.Handlers;
using Missions.Services.Agents.Patches;
using Missions.Services.Arena;
using Missions.Services.Missiles;
using Missions.Services.Missiles.Handlers;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
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
    public class CoopArenaController : MissionBehavior, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly INetworkMessageBroker networkMessageBroker;
        private readonly INetworkAgentRegistry agentRegistry;
        private readonly IRandomEquipmentGenerator equipmentGenerator;
        private readonly IBinaryPackageFactory packageFactory;
        private readonly IDisposable[] handlers;

        private List<MatrixFrame> spawnFrames = new List<MatrixFrame>();
        private CharacterObject[] gameCharacters;
        private readonly Guid playerId;

        public CoopArenaController(
            INetworkMessageBroker networkMessageBroker,
            INetworkAgentRegistry agentRegistry, 
            IRandomEquipmentGenerator equipmentGenerator,
            IBinaryPackageFactory packageFactory,
            IMissileHandler missileHandler,
            IWeaponDropHandler weaponDropHandler,
            IWeaponPickupHandler weaponPickupHandler,
            IShieldDamageHandler shieldDamageHandler,
            IAgentDamageHandler agentDamageHandler,
            INetworkMissileRegistry networkMissileRegistry)
        {
            this.networkMessageBroker = networkMessageBroker;
            this.agentRegistry = agentRegistry;
            this.equipmentGenerator = equipmentGenerator;
            this.packageFactory = packageFactory;

            handlers = new IDisposable[]
            {
                missileHandler,
                weaponDropHandler,
                weaponPickupHandler,
                shieldDamageHandler,
                agentDamageHandler,
                networkMissileRegistry,
            };

            playerId = Guid.NewGuid();

            this.networkMessageBroker.Subscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            this.networkMessageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);
            this.networkMessageBroker.Subscribe<AgentDied>(Handle_AgentDeath);
        }

        ~CoopArenaController()
        {
            Dispose();
        }

        public void Dispose()
        {
            agentRegistry.Clear();

            foreach (var handler in handlers)
            {
                handler.Dispose();
            }

            networkMessageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            networkMessageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
            networkMessageBroker.Unsubscribe<AgentDied>(Handle_AgentDeath);
        }

        public override void AfterStart()
        {
            gameCharacters = CharacterObject.All?.Where(x =>
            x.IsHero == false &&
            x.Age > 18).ToArray();
            AddPlayerToArena();
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> payload)
        {
            SendJoinInfo(payload.What.Peer);
        }

        private void SendJoinInfo(NetPeer peer)
        {
            CharacterObject characterObject = CharacterObject.PlayerCharacter;

            List<Vec3> unitPositions = new List<Vec3>();
            List<string> unitIdStrings = new List<string>();
            List<Guid> guids = new List<Guid>();
            List<float> unitHealths = new List<float>();
            foreach (Guid agentId in agentRegistry.ControlledAgents.Keys)
            {
                Agent agent = agentRegistry.ControlledAgents[agentId];

                if (agent == Agent.Main) continue;

                guids.Add(agentId);
                unitPositions.Add(agent.Position);
                unitIdStrings.Add(agent.Character.StringId);
                unitHealths.Add(agent.Health);
            }

            Logger.Debug("Sending join request");

            bool isPlayerAlive = Agent.Main != null && Agent.Main.Health > 0;
            Vec3 position = Agent.Main?.Position ?? default;
            float health = Agent.Main?.Health ?? 0;
            NetworkMissionJoinInfo request = new NetworkMissionJoinInfo(characterObject, isPlayerAlive, playerId, position, health, guids.ToArray(), unitPositions.ToArray(), unitIdStrings.ToArray(), unitHealths.ToArray());
            networkMessageBroker.PublishNetworkEvent(peer, request);
            Logger.Information("Sent {AgentType} Join Request for {AgentName}({PlayerID}) to {Peer}",
                characterObject.IsPlayerCharacter ? "Player" : "Agent",
                characterObject.Name, request.PlayerId, peer.EndPoint);
        }

        private void Handle_JoinInfo(MessagePayload<NetworkMissionJoinInfo> payload)
        {
            Logger.Debug("Received join request");
            NetPeer netPeer = (NetPeer)payload.Who;

            NetworkMissionJoinInfo joinInfo = payload.What;

            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer} with {ControlledAgentCount} controlled agents",
                joinInfo.CharacterObject.IsPlayerCharacter ? "Player" : "Agent",
                joinInfo.CharacterObject.Name, newAgentId,
                netPeer.EndPoint,
                joinInfo.UnitIdString?.Length);

            if (joinInfo.IsPlayerAlive)
            {
                Agent newAgent = SpawnAgent(startingPos, joinInfo.CharacterObject, true, joinInfo.Equipment);
                newAgent.Health = joinInfo.PlayerHealth;

                agentRegistry.RegisterNetworkControlledAgent(netPeer, joinInfo.PlayerId, newAgent);
            }

            for (int i = 0; i < joinInfo.UnitIdString?.Length; i++)
            {
                SpawnAIAgent(
                    netPeer,
                    joinInfo.UnitIdString[i],
                    joinInfo.UnitStartingPosition[i],
                    joinInfo.UnitHealthList[i],
                    joinInfo.UnitId[i]);
            }

            networkMessageBroker.Publish(this, new PeerReady(netPeer));
        }

        private void SpawnAIAgent(
            NetPeer controller, 
            string characterStringId, 
            Vec3 startingPos, 
            float health, 
            Guid unitId)
        {
            var AICharacter = CharacterObject.Find(characterStringId);

            if (AICharacter == null)
            {
                Logger.Error("Could not find character with stringID: {stringid}", characterStringId);
                return;
            }

            Agent tempAi = SpawnAgent(startingPos, AICharacter, true);
            tempAi.Health = health;

            agentRegistry.RegisterNetworkControlledAgent(controller, unitId, tempAi);
        }

        private void Handle_AgentDeath(MessagePayload<AgentDied> obj)
        {
            Agent agent = obj.What.Agent;
            if (agentRegistry.TryGetAgentId(agent, out Guid agentId))
            {
                agentRegistry.RemoveControlledAgent(agentId);
                agentRegistry.RemoveNetworkControlledAgent(agentId);
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

            Agent ai = SpawnAgent(randomElement.origin, gameCharacters[rand.Next(gameCharacters.Length - 1)], false);

            agentRegistry.RegisterControlledAgent(playerId, Agent.Main);
            agentRegistry.RegisterControlledAgent(Guid.NewGuid(), ai);
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
            Equipment generatedEquipment = equipmentGenerator.CreateRandomEquipment(true);
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

        public Agent SpawnAgent(Vec3 startingPos, CharacterObject character, bool isEnemy, Equipment equipment = null)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData.InitialPosition(startingPos);
            agentBuildData.Team(isEnemy ? Mission.Current.PlayerEnemyTeam : Mission.Current.PlayerTeam);
            agentBuildData.InitialDirection(Vec2.Forward);
            agentBuildData.NoHorses(true);
            agentBuildData.Equipment(equipment ?? (character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment));
            if(equipment != null)
            {
                MissionEquipment missionEquipment = new MissionEquipment();
                missionEquipment.FillFrom(equipment, new Banner());
                agentBuildData.MissionEquipment(missionEquipment);
            }
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
            Dispose();
        }
    }
}