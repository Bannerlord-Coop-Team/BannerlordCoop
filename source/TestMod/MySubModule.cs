using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.SaveSystem;
using TaleWorlds.InputSystem;
using System.IO;
using TaleWorlds.Library;
using System.Linq;
using TaleWorlds.Engine;
using System.Threading;
using TaleWorlds.SaveSystem.Load;
using HarmonyLib;
using NetworkMessages.FromServer;
using SandBox;
using LiteNetLib;
using MissionsShared;
using ProtoBuf;
using System.Collections.Concurrent;
using LiteNetLib.Utils;

namespace CoopTestMod
{
    

    public class MySubModule : MBSubModuleBase
    {
        MissionNetworkBehavior networkBehavior = new MissionNetworkBehavior();
        private bool subModuleLoaded = false;
        private bool battleLoaded = false;
        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            mission.AddMissionBehavior(networkBehavior);
        }


        protected override void OnApplicationTick(float dt)
        {
            if (!battleLoaded && Mission.Current != null && Mission.Current.IsLoadingFinished)
            {
                // again theres gotta be a better way to check if missions finish loading? A custom mission maybe in the future
                battleLoaded = true;
                networkBehavior.StartArenaFight();
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
        }
    }
}