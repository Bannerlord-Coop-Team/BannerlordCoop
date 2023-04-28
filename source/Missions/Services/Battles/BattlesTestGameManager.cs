using Autofac;
using Common;
using Common.Logging;
using Missions.Services.Network;
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
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;

namespace Missions.Services.Arena
{
    public class BattlesTestGameManager : SandBoxGameManager, IMissionGameManager
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BattlesTestGameManager>();
        
        private readonly LiteNetP2PClient _client;
        private readonly CoopBattlesController _battlesController;
        private readonly CoopMissionNetworkBehavior _networkBehavior;

        public BattlesTestGameManager(LoadResult loadedGameResult, 
            LiteNetP2PClient client,
            CoopBattlesController battlesController,
            CoopMissionNetworkBehavior networkBehavior) : base(loadedGameResult)
        {
            _client = client;
            _battlesController = battlesController;
            _networkBehavior = networkBehavior;
        }

        public void StartGame()
        {
            if (_client.ConnectToP2PServer())
            {
                StartNewGame(this);
            }
            else
            {
                ScreenManager.PopScreen();
                // TODO add popup message
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

            Location arena = settlement.LocationComplex.GetLocationWithId("arena");

            //return arena scenae name of current town
            int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;

            //Open a new arena mission with the scene; commented out because we are not doing Arena testing right now
            StartArenaMission(arena, upgradeLevel);

            MouseManager.ShowCursor(false);
        }

        private void StartArenaMission(Location location, int upgradeLevel)
        {
            NetworkAgentRegistry.Instance.Clear();

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
                _networkBehavior,
                _battlesController,
                //ViewCreator.CreateMissionOrderUIHandler(),
            }, true, true);
        }

        public static string[] GetAllSpawnPoints(Scene scene)
        {
            List<GameEntity> entities = new List<GameEntity>();
            scene.GetEntities(ref entities);
            return entities.Where(entity => entity.Tags.Any(tag => tag.StartsWith("sp_"))).Select(entity => entity.Name).ToArray();
        }
    }
}
