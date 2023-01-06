using Common;
using Coop.Mod;
using Coop.Mod.Missions;
using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
    {
	    private static ILogger Logger;
		private static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        
        protected override void OnSubModuleLoad()
        {
	        if (System.Diagnostics.Debugger.IsAttached)
	        {
		        LogManager.Configuration
			        .Enrich.WithProcessId()
			        .WriteTo.Debug(
				        outputTemplate:
						"[({ProcessId}) {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
			        .MinimumLevel.Verbose();
	        }

	        Logger = LogManager.GetLogger<TestMod>();
			Logger.Verbose("Building Network Configuration");

			Updateables.Add(GameLoopRunner.Instance);

            JoinTavern = new InitialStateOption(
              "Join Online Tavern",
              new TextObject("Join Online Tavern"),
              9991,
              StartClientInTavern,
              () => (false, new TextObject()));

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            base.OnSubModuleLoad();
            Logger.Verbose("Bannerlord Coop Mod loaded");
        }

        private bool m_IsFirstTick = true;
        protected override void OnApplicationTick(float dt)
        {
            if (m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();

                m_IsFirstTick = false;
            }
            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);
        }

        private static void StartClientInTavern()
        {
	        SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles(null);
            SaveGameFileInfo save = saveFiles.FirstOrDefault(s => ValidateModules(s.MetaData));

            if(save == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Unable to find a save without Mods. Create a fresh game and try again."));
            }
            else
            {
                SandBoxSaveHelper.TryLoadSave(save, StartGame, null);
            }
        }

        private static readonly HashSet<string> allowedModules = new HashSet<string>()
        {
            "Native",
            "Sandbox",
            "SandBox Core",
            "StoryMode",
            "CustomBattle",
            "BirthAndDeath",
            "MissionTestMod",
        };
        private static bool ValidateModules(MetaData metaData)
        {
            if(metaData == null) return false;

            var moduleNames = metaData.GetModules();
            if (moduleNames.Any(name => !allowedModules.Contains(name))) return false;
            
            return true;
        }

        private static void StartGame(LoadResult loadResult)
        {
            MissionTestGameManager manager = new MissionTestGameManager(loadResult);
            manager.StartGameInTavern();
        }
    }
}
