//using Common;
//using Common.Logging;
//using Common.Messaging;
//using GameInterface.Missions.Agents.Handlers;
//using GameInterface.Missions.Arena;
//using GameInterface.Missions.Missiles;
//using GameInterface.Missions.Missiles.Handlers;
//using GameInterface.Missions.Services.Network;
//using GameInterface.Missions.Services.Network.Data;
//using GameInterface.Missions.Services.Network.Messages;
//using GameInterface.Serialization;
//using GameInterface.Services.ObjectManager;
//using LiteNetLib;
//using Serilog;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.AgentOrigins;
//using TaleWorlds.Core;
//using TaleWorlds.Library;
//using TaleWorlds.MountAndBlade;

//namespace GameInterface.Missions.Battles
//{
//    /// <summary>
//    /// Mission Controller that does all the logic in the Coop Battles
//    /// </summary>
//    public class CoopBattlesController : CoopMissionController
//    {
//        private static readonly ILogger Logger = LogManager.GetLogger<CoopBattlesController>();

//        private readonly IRandomEquipmentGenerator equipmentGenerator;
//        private readonly IBinaryPackageFactory packageFactory;

//        private List<MatrixFrame> spawnFrames = new List<MatrixFrame>();
//        private CharacterObject[] gameCharacters;
//        private readonly string playerId;

//        public CoopBattlesController(
//            IMessageBroker messageBroker,
//            IObjectManager objectManager,
//            IBattleNetwork network,
//            INetworkAgentRegistry agentRegistry,
//            IRandomEquipmentGenerator equipmentGenerator,
//            IBinaryPackageFactory packageFactory,
//            IMissileHandler missileHandler,
//            IWeaponDropHandler weaponDropHandler,
//            IWeaponPickupHandler weaponPickupHandler,
//            IShieldDamageHandler shieldDamageHandler,
//            IAgentDamageHandler agentDamageHandler,
//            IAgentDeathHandler agentDeathHandler,
//            INetworkMissileRegistry networkMissileRegistry)
//            : base(network, messageBroker, objectManager, agentRegistry, new IDisposable[]
//            {
//                missileHandler,
//                weaponDropHandler,
//                weaponPickupHandler,
//                shieldDamageHandler,
//                agentDamageHandler,
//                agentDeathHandler,
//                networkMissileRegistry,
//            })
//        {
//            this.equipmentGenerator = equipmentGenerator;
//            this.packageFactory = packageFactory;

//            playerId = Guid.NewGuid().ToString();
//        }

//        ~CoopBattlesController()
//        {
//            Dispose();
//        }

//        public override void AfterStart()
//        {
//            gameCharacters = CharacterObject.All?.Where(x =>
//            x.IsHero == false &&
//            x.Age > 18).ToArray();
//            AddPlayerToArena();
//        }

//        protected override void SendJoinInfo(string controllerId)
//        {
//            CharacterObject characterObject = CharacterObject.PlayerCharacter;

//            List<AiAgentData> aiAgentDatas = new List<AiAgentData>();

//            foreach (string agentId in agentRegistry.ControlledAgents.Keys)
//            {
//                Agent agent = agentRegistry.ControlledAgents[agentId];

//                if (agent == Agent.Main) continue;

//                AiAgentData aiAgentData = new AiAgentData(
//                    agentId,
//                    agent.Position,
//                    agent.Character.StringId,
//                    agent.Health);


//                aiAgentDatas.Add(aiAgentData);
//            }

//            Logger.Debug("Sending join request");

//            bool isPlayerAlive = Agent.Main != null && Agent.Main.Health > 0;
//            Vec3 position = Agent.Main?.Position ?? default;
//            float health = Agent.Main?.Health ?? 0;

//            if (!objectManager.TryGetIdWithLogging(CharacterObject.PlayerCharacter, out var characterObjectId))
//                return;

//            NetworkMissionJoinInfo request = new NetworkMissionJoinInfo(
//                characterObjectId,
//                isPlayerAlive,
//                playerId,
//                position,
//                health,
//                aiAgentDatas.ToArray());

//            network.Send(controllerId, request);
//            Logger.Information("Sent {AgentType} Join Request for {AgentName}({PlayerID}) to {Controller}",
//                characterObject.IsPlayerCharacter ? "Player" : "Agent",
//                characterObject.Name, request.ControllerId, controllerId);
//        }

//        protected override void HandleJoinInfo(NetPeer netPeer, NetworkMissionJoinInfo joinInfo)
//        {
//            Logger.Debug("Received join request");

//            string newAgentId = joinInfo.ControllerId;
//            Vec3 startingPos = joinInfo.StartingPosition;

//            if (!objectManager.TryGetObjectWithLogging(joinInfo.CharacterObjectId, out CharacterObject characterObject))
//                return;

//            try
//            {
//                Logger.Information("Spawning Player called {AgentName}({AgentID}) from {Peer} with {ControlledAgentCount} controlled agents",
//                characterObject.Name,
//                newAgentId,
//                netPeer,
//                joinInfo?.AiAgentData?.Length);
//            }
//            catch (Exception) { }

//            if (joinInfo.IsPlayerAlive)
//            {
//                Agent newAgent = SpawnAgent(startingPos, characterObject, true, characterObject.Equipment);

//                newAgent.Health = joinInfo.PlayerHealth;

//                agentRegistry.RegisterNetworkControlledAgent(netPeer, joinInfo.ControllerId, newAgent);
//            }

//            for (int i = 0; i < joinInfo.AiAgentData?.Length; i++)
//            {
//                AiAgentData aiAgentData = joinInfo.AiAgentData[i];
//                SpawnAIAgent(netPeer, aiAgentData);
//            }

//            messageBroker.Publish(this, new PeerReady(netPeer));
//        }

//        private void SpawnAIAgent(
//            NetPeer controller,
//            AiAgentData agentData)
//        {
//            var AICharacter = CharacterObject.Find(agentData.UnitIdString);

//            if (AICharacter == null)
//            {
//                Logger.Error("Could not find character with stringID: {stringid}", agentData.UnitIdString);
//                return;
//            }

//            Agent aiAgent = SpawnAgent(agentData.UnitPosition, AICharacter, true);
//            aiAgent.Health = agentData.UnitHealth;

//            aiAgent.SetWatchState(Agent.WatchState.Alarmed);

//            agentRegistry.RegisterNetworkControlledAgent(controller, agentData.UnitId, aiAgent);
//        }

//        private void AddPlayerToArena()
//        {
//            // reset teams if any exists
//            Mission.Current.ResetMission();

//            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
//            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

//            // players is attacker team
//            Mission.Current.PlayerTeam = Mission.Current.AttackerTeam;

//            spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_player")
//                           select e.GetGlobalFrame()).ToList();
//            for (int i = 0; i < spawnFrames.Count; i++)
//            {
//                MatrixFrame value = spawnFrames[i];
//                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
//                spawnFrames[i] = value;
//            }

//            // get a random spawn point
//            Random rand = new Random();
//            MatrixFrame randomElement = spawnFrames[rand.Next(spawnFrames.Count)];

//            // spawn an instance of the player (controlled by default)
//            Agent player = SpawnPlayerAgent(CharacterObject.PlayerCharacter, randomElement);

//            Agent.Main.SetTeam(Mission.Current.PlayerTeam, false);

//            Agent ai = SpawnAgent(randomElement.origin, gameCharacters[rand.Next(gameCharacters.Length - 1)], false);

//            agentRegistry.RegisterControlledAgent(playerId, Agent.Main);
//            agentRegistry.RegisterControlledAgent(Guid.NewGuid().ToString(), ai);
//        }

//        private static readonly PropertyInfo Hero_BattleEquipment = typeof(Hero).GetProperty("BattleEquipment", BindingFlags.Public | BindingFlags.Instance);
//        /// <summary>
//        /// Spawn an agent based on its character object and frame. For now, Main agent character object is used
//        /// This should be the real character object in the future
//        /// </summary>
//        private Agent SpawnPlayerAgent(CharacterObject character, MatrixFrame frame)
//        {
//            AgentBuildData agentBuildData = new AgentBuildData(character);
//            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
//            agentBuildData = agentBuildData.Team(Mission.Current.PlayerTeam).InitialPosition(frame.origin);
//            agentBuildData.NoHorses(true);

//            Vec2 vec = frame.rotation.f.AsVec2;
//            vec = vec.Normalized();
//            Equipment generatedEquipment = equipmentGenerator.CreateRandomEquipment(true);
//            agentBuildData.Equipment(generatedEquipment);
//            Hero_BattleEquipment.SetValue(character.HeroObject, generatedEquipment);
//            agentBuildData.InitialDirection(vec);
//            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
//            agentBuildData.Controller(AgentControllerType.Player);

//            Agent agent = default;
//            GameThread.Run(() =>
//            {
//                agent = Mission.Current.SpawnAgent(agentBuildData);
//                agent.FadeIn();
//            }, true);
//            agent.FadeIn();

//            return agent;
//        }

//        public Agent SpawnAgent(Vec3 startingPos, CharacterObject character, bool isEnemy, Equipment equipment = null)
//        {
//            AgentBuildData agentBuildData = new AgentBuildData(character);
//            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
//            agentBuildData.InitialPosition(startingPos);
//            agentBuildData.Team(isEnemy ? Mission.Current.PlayerEnemyTeam : Mission.Current.PlayerTeam);
//            agentBuildData.InitialDirection(Vec2.Forward);
//            agentBuildData.NoHorses(true);
//            agentBuildData.Equipment(equipment ?? (character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment));
//            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
//            agentBuildData.Controller(isEnemy ? AgentControllerType.None : AgentControllerType.AI);

//            Agent agent = default;
//            GameThread.Run(() =>
//            {
//                agent = Mission.Current.SpawnAgent(agentBuildData);
//                agent.FadeIn();
//            }, true);

//            return agent;
//        }
//    }
//}
