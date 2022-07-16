using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.InputSystem;
using TaleWorlds.SaveSystem.Load;
using System.Reflection;
using SandBox.BoardGames.MissionLogics;

namespace CoopTestMod
{
    

    public class MySubModule : MBSubModuleBase
    {
        // initialize the network connection
        MissionNetworkBehavior networkBehavior;
        private bool subModuleLoaded = false;
        private bool battleLoaded = false;
        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            // add the network behavior
            mission.AddMissionBehavior(networkBehavior);
        }

        protected override void OnSubModuleLoad()
        {
            networkBehavior = new MissionNetworkBehavior();
            //DEBUG: skip intro
            FieldInfo splashScreen = TaleWorlds.MountAndBlade.Module.CurrentModule.GetType().GetField("_splashScreenPlayed", BindingFlags.Instance | BindingFlags.NonPublic);
            splashScreen.SetValue(TaleWorlds.MountAndBlade.Module.CurrentModule, true);
        }


        protected override void OnApplicationTick(float dt)
        {

            // NOTE
            // ALL THE CODE BELOW IS FOR DEBUGGING AND AUTO LOADING A SAVE
            //


            if (!battleLoaded && Mission.Current != null && Mission.Current.IsLoadingFinished)
            {
                // again theres gotta be a better way to check if missions finish loading? A custom mission maybe in the future
                battleLoaded = true;
                //networkBehavior.StartArenaFight();
            }

            if (!subModuleLoaded && TaleWorlds.MountAndBlade.Module.CurrentModule.LoadingFinished)
            {
                // sub module is loaded. Isnt there a proper callback for this?
                subModuleLoaded = true;

                //Get all the save sames
                SaveGameFileInfo[] games = MBSaveLoad.GetSaveFiles();


                // just load the first one. 
                LoadResult result = SaveManager.Load(games[0].Name, new AsyncFileSaveDriver(), true);

                // Create our own game manager. This will help us override the OnLoaded callback and load the town
                CustomSandboxGame manager = new CustomSandboxGame(result);

                //start it
                MBGameManager.StartNewGame(manager);
            }

            // debug numpad 6 output
            if (Input.IsKeyReleased(InputKey.Numpad6))
            {

                InformationManager.DisplayMessage(new InformationMessage("There are ticks for: " + networkBehavior.GetPlayerSyncDict().Count.ToString()));
                foreach (int clientId in networkBehavior.GetPlayerSyncDict().Keys)
                {
                    foreach (string info in networkBehavior.GetPlayerSyncDict()[clientId].Keys)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("Agent " + info + " from " + clientId));
                    }
                }

            }
            if (Input.IsKeyReleased(InputKey.Numpad7)) 
            {
                MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

                InformationManager.DisplayMessage(new InformationMessage(boardGameLogic.OpposingAgent.Index.ToString()));
            }
        }
    }
}