using Common.Messaging;
using HarmonyLib;
using IntroServer.Config;
using ProtoBuf.Meta;
using SandBox;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem.Load;
using Missions.Services.Network;
using Missions.Services.Network.Surrogates;

namespace Missions.Services
{
    public class MissionTestGameManager : SandBoxGameManager
    {
        static MissionTestGameManager()
        {
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
        }

        private static readonly ILogger Logger = LogManager.GetLogger<MissionTestGameManager>();
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");
        private LiteNetP2PClient m_Client;

        public MissionTestGameManager(LoadResult loadedGameResult) : base(loadedGameResult)
        {
            harmony.PatchAll();
        }

        ~MissionTestGameManager()
        {
        }

        public void StartGameInTavern()
        {
            NetworkConfiguration config = new NetworkConfiguration();

            m_Client = new LiteNetP2PClient(config, MessageBroker.Instance);

            if(m_Client.ConnectToP2PServer())
            {
                StartNewGame(this);
            }
            else
            {
                Logger.Error("Server Unreachable");
            }
        }

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            //get the settlement first
            Settlement settlement = Settlement.Find("town_ES3");

            CharacterObject characterObject = CharacterObject.PlayerCharacter;
            LocationEncounter locationEncounter = new TownEncounter(settlement);

            // create an encounter of the town with the player
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);

            //Set our encounter to the created encounter
            PlayerEncounter.LocationEncounter = locationEncounter;

            int upgradeLevel = settlement.Town?.GetWallLevel() ?? 1;
            Location tavern = LocationComplex.Current.GetLocationWithId("tavern");
            string scene = tavern.GetSceneName(upgradeLevel);
            Mission mission = SandBoxMissions.OpenIndoorMission(scene, tavern);
            mission.AddMissionBehavior(new MissionNetworkBehavior(m_Client, MessageBroker.Instance));

            //PlayerEncounter.EnterSettlement();

            //Location center = settlement.LocationComplex.GetLocationWithId("center");

            //return arena scenae name of current town
            //         int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;

            //         //Open a new arena mission with the scene; commented out because we are not doing Arena testing right now
            //string civilianUpgradeLevelTag = Campaign.Current.Models.LocationModel.GetCivilianUpgradeLevelTag(upgradeLevel);
            //         Mission currentMission = MissionState.OpenNew("ArenaDuelMission", SandBoxMissions.CreateSandBoxMissionInitializerRecord(locationWithId.GetSceneName(upgradeLevel), "", false), (Mission mission) => new MissionBehavior[]
            //            {
            //                             new MissionOptionsComponent(),
            //                             //new ArenaDuelMissionController(CharacterObject.PlayerCharacter, false, false, null, 1), //this was the default controller that spawned the player and 1 opponent. Not very useful
            //                             new MissionFacialAnimationHandler(),
            //                             new MissionDebugHandler(),
            //                             new MissionAgentPanicHandler(),
            //                             new AgentCommonAILogic(),
            //                             new AgentHumanAILogic(),
            //                             new ArenaAgentStateDeciderLogic(),
            //                             new VisualTrackerMissionBehavior(),
            //                             new CampaignMissionComponent(),
            //                             new MissionNetworkComponent(),
            //                             new EquipmentControllerLeaveLogic(),
            //                             new MissionAgentHandler(locationWithId, null),
            //                             new MissionNetworkBehavior(),
            //            }, true, true);

            //MouseManager.ShowCursor(false);


        }

        // Spawn an agent based on its character object and frame. For now, Main agent character object is used
        // This should be the real character object in the future
        private static Agent SpawnAgent(CharacterObject character, MatrixFrame frame)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData = agentBuildData.Team(Mission.Current.PlayerAllyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);
            agent.FadeIn();
            agent.Controller = Agent.ControllerType.None;
            return agent;
        }

        // DEBUG METHOD: To spawn in Arena and test fights
        private static Agent SpawnArenaAgent(CharacterObject character, MatrixFrame frame, bool isMain)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            Mission mission = Mission.Current;
            agentBuildData = agentBuildData.Team(isMain ? Mission.Current.PlayerTeam : Mission.Current.PlayerEnemyTeam).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            Agent agent = mission.SpawnAgent(agentBuildData.InitialDirection(vec).NoHorses(true).Equipment(character.FirstBattleEquipment).TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor))), false, 0);                             //this spawns an archer
            agent.FadeIn();

            if (isMain)
            {
                agent.Controller = Agent.ControllerType.Player;
            }
            else
            {
                agent.Controller = Agent.ControllerType.AI;
            }

            return agent;
        }

        public static Agent AddPlayerToArena(bool isMain)
        {
            Mission.Current.PlayerTeam = Mission.Current.AttackerTeam;

            List<MatrixFrame> spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }
            //// get a random spawn point
            MatrixFrame randomElement = spawnFrames.GetRandomElement();
            ////remove the point so no overlap
            //_initialSpawnFrames.Remove(randomElement);
            ////find another spawn point
            //randomElement2 = randomElement;


            //// spawn an instance of the player (controlled by default)
            return SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, isMain);
        }

        // DEBUG METHOD: Starts an Arena fight
        public static void StartArenaFight()
        {
            //reset teams if any exists

            Mission.Current.ResetMission();

            //
            Mission.Current.Teams.Add(BattleSideEnum.Defender, Hero.MainHero.MapFaction.Color, Hero.MainHero.MapFaction.Color2, null, true, false, true);
            Mission.Current.Teams.Add(BattleSideEnum.Attacker, Hero.MainHero.MapFaction.Color2, Hero.MainHero.MapFaction.Color, null, true, false, true);

            //players is defender team
            Mission.Current.PlayerTeam = Mission.Current.DefenderTeam;


            //find areas of spawn

            List<MatrixFrame> spawnFrames = (from e in Mission.Current.Scene.FindEntitiesWithTag("sp_arena")
                                             select e.GetGlobalFrame()).ToList();
            for (int i = 0; i < spawnFrames.Count; i++)
            {
                MatrixFrame value = spawnFrames[i];
                value.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                spawnFrames[i] = value;
            }
            //// get a random spawn point
            MatrixFrame randomElement = spawnFrames.GetRandomElement();
            ////remove the point so no overlap
            //_initialSpawnFrames.Remove(randomElement);
            ////find another spawn point
            //randomElement2 = randomElement;


            //// spawn an instance of the player (controlled by default)
            SpawnArenaAgent(CharacterObject.PlayerCharacter, randomElement, true);

        }

        public static Agent SpawnAgent(Vec3 startingPos, CharacterObject character)
        {
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData.InitialPosition(startingPos);
            agentBuildData.Team(Mission.Current.PlayerAllyTeam);
            agentBuildData.InitialDirection(Vec2.Forward);
            agentBuildData.NoHorses(true);
            agentBuildData.Equipment(character.FirstCivilianEquipment);
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor)));
            agentBuildData.Controller(Agent.ControllerType.None);

            Agent agent = default(Agent);
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            });

            return agent;
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("spawn_tavern_agent", "test")]
        public static Agent SpawnTavernAgent()
        {
            MatrixFrame frame = Mission.Current.Scene.FindEntitiesWithTag("sp_player_conversation").Single().GetGlobalFrame();

            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();

            CharacterObject character = Hero.MainHero.CharacterObject;
            AgentBuildData agentBuildData = new AgentBuildData(character);
            agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
            agentBuildData.InitialPosition(frame.origin);
            agentBuildData.Team(Mission.Current.PlayerAllyTeam);
            agentBuildData.InitialDirection(vec);
            agentBuildData.NoHorses(true);
            agentBuildData.Equipment(character.FirstCivilianEquipment);
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default(UniqueTroopDescriptor)));
            agentBuildData.Controller(Agent.ControllerType.None);

            Logger.Information("Spawning Agent");
            Agent agent = default;
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            });

            return agent;
        }

        public static string[] GetAllSpawnPoints(Scene scene)
        {
            List<GameEntity> entities = new List<GameEntity>();
            scene.GetEntities(ref entities);
            return entities.Where(entity => entity.Tags.Any(tag => tag.StartsWith("sp_"))).Select(entity => entity.Name).ToArray();
        }

        public override void OnGameEnd(Game game)
        {
            harmony.UnpatchAll();
            base.OnGameEnd(game);
        }
    }
}
