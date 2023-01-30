using Common;
using Common.Logging;
using Common.Network;
using HarmonyLib;
using IntroServer.Config;
using Missions.Services.Network;
using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using SandBox;
using Serilog;
using System;
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
using TaleWorlds.SaveSystem.Load;

namespace Missions.Services.Taverns
{
    public class TavernsGameManager : SandBoxGameManager, IMissionGameManager
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TavernsGameManager>();
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");
        private LiteNetP2PClient _client;

        public TavernsGameManager(LoadResult loadedGameResult) : base(loadedGameResult)
        {
            harmony.PatchAll();
        }

        public void StartGame()
        {
            NetworkConfiguration config = new NetworkConfiguration();

            _client = new LiteNetP2PClient(config);

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

            LocationEncounter locationEncounter = new TownEncounter(settlement);

            // create an encounter of the town with the player
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);

            //Set our encounter to the created encounter
            PlayerEncounter.LocationEncounter = locationEncounter;

            int upgradeLevel = settlement.Town?.GetWallLevel() ?? 1;
            Location tavern = LocationComplex.Current.GetLocationWithId("tavern");
            string scene = tavern.GetSceneName(upgradeLevel);
            Mission mission = SandBoxMissions.OpenIndoorMission(scene, tavern);
            mission.AddMissionBehavior(new CoopMissionNetworkBehavior(_client, NetworkMessageBroker.Instance, NetworkAgentRegistry.Instance));
            mission.AddMissionBehavior(new CoopTavernsController(_client, NetworkMessageBroker.Instance, NetworkAgentRegistry.Instance));
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
            agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
            agentBuildData.Controller(Agent.ControllerType.None);

            Logger.Information("Spawning Agent");
            Agent agent = default;
            GameLoopRunner.RunOnMainThread(() =>
            {
                agent = Mission.Current.SpawnAgent(agentBuildData);
                agent.FadeIn();
            }, true);

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
