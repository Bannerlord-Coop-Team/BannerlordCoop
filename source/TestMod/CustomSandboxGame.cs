using SandBox;
using System;
using TaleWorlds.CampaignSystem;
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
using TaleWorlds.SaveSystem.Load;

namespace CoopTestMod
{
    internal class CustomSandboxGame : SandBoxGameManager
    {

        delegate void PositionRefDelegate(UIntPtr agentPtr, ref Vec3 position);



        public CustomSandboxGame(LoadResult loadedGameResult) : base(loadedGameResult)
        {
        }

        public override void OnLoadFinished()
        {
            base.OnLoadFinished();
            //get the settlement first
            Settlement settlement = Settlement.Find("town_ES3");

            // get its arena
            Location locationWithId = settlement.LocationComplex.GetLocationWithId("arena");

            CharacterObject characterObject = CharacterObject.PlayerCharacter;
            LocationEncounter locationEncounter = new TownEncounter(settlement);

            // create an encounter of the town with the player
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);

            //Set our encounter to the created encounter
            PlayerEncounter.LocationEncounter = locationEncounter;

            //return arena scenae name of current town
            int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;

            //Open a new arena mission with the scene; commented out because we are not doing Arena testing right now
            //Mission currentMission = MissionState.OpenNew("ArenaDuelMission", SandBoxMissions.CreateSandBoxMissionInitializerRecord(locationWithId.GetSceneName(upgradeLevel), "", false), (Mission mission) => new MissionBehavior[]
            //   {
            //                    new MissionOptionsComponent(),
            //                    //new ArenaDuelMissionController(CharacterObject.PlayerCharacter, false, false, null, 1), //this was the default controller that spawned the player and 1 opponent. Not very useful
            //                    new MissionFacialAnimationHandler(),
            //                    new MissionDebugHandler(),
            //                    new MissionAgentPanicHandler(),
            //                    new AgentCommonAILogic(),
            //                    new AgentHumanAILogic(),
            //                    new ArenaAgentStateDeciderLogic(),
            //                    new VisualTrackerMissionBehavior(),
            //                    new CampaignMissionComponent(),
            //                    new MissionNetworkComponent(),
            //                    new EquipmentControllerLeaveLogic(),
            //                    new MissionAgentHandler(locationWithId, null)
            //   }, true, true);

            //MouseManager.ShowCursor(false);


        }
    }
}
