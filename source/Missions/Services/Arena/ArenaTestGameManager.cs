using Common.Logging;
using Common.Messaging;
using HarmonyLib;
using IntroServer.Config;
using Missions.Services.Network;
using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using SandBox;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using Serilog;
using System.Collections.Generic;
using System.Linq;
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
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem.Load;

namespace Missions.Services.Arena
{
    public class ArenaTestGameManager : SandBoxGameManager, IMissionGameManager
    {
        static ArenaTestGameManager()
        {
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
        }

        private static readonly ILogger Logger = LogManager.GetLogger<ArenaTestGameManager>();
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");
        private LiteNetP2PClient _client;

        public ArenaTestGameManager(LoadResult loadedGameResult) : base(loadedGameResult)
        {
            harmony.PatchAll();
        }

        ~ArenaTestGameManager()
        {
        }

        public void StartGame()
        {
            NetworkConfiguration config = new NetworkConfiguration();

            _client = new LiteNetP2PClient(config, MessageBroker.Instance);

            if (_client.ConnectToP2PServer())
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

            Location arena = settlement.LocationComplex.GetLocationWithId("arena");

            //return arena scenae name of current town
            int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;

            //Open a new arena mission with the scene; commented out because we are not doing Arena testing right now
            StartArenaMission(arena, upgradeLevel);

            MouseManager.ShowCursor(false);
        }

        private void StartArenaMission(Location location, int upgradeLevel)
        {
            string civilianUpgradeLevelTag = Campaign.Current.Models.LocationModel.GetCivilianUpgradeLevelTag(upgradeLevel);
            Mission currentMission = MissionState.OpenNew("ArenaDuelMission", SandBoxMissions.CreateSandBoxMissionInitializerRecord(location.GetSceneName(upgradeLevel), "", false), (mission) => new MissionBehavior[]
            {
                new MissionOptionsComponent(),
                new MissionFacialAnimationHandler(),
                new MissionDebugHandler(),
                new MissionAgentPanicHandler(),
                new AgentHumanAILogic(),
                new ArenaAgentStateDeciderLogic(),
                new VisualTrackerMissionBehavior(),
                new CampaignMissionComponent(),
                new EquipmentControllerLeaveLogic(),
                new MissionAgentHandler(location),
                new CoopMissionNetworkBehavior(_client, MessageBroker.Instance, NetworkAgentRegistry.Instance),
                new CoopArenaController(MessageBroker.Instance, NetworkAgentRegistry.Instance, new RandomEquipmentGenerator()),
                //ViewCreator.CreateMissionOrderUIHandler(),
            }, true, true);
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
