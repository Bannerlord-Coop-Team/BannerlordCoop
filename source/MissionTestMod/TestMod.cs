using Common;
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
using Missions;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
    {
	    private static ILogger Logger;
		private static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        private static InitialStateOption JoinArena;
        private static MissionTestGameManager tavernManager;
        private static ArenaTestGameManager arenaManager;

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

            JoinArena = new InitialStateOption(
                "Join Online Arena",
                 new TextObject("Join Online Arena"),
                 9990,
                 StartClientInArena,
                 () => (false, new TextObject()));

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            Module.CurrentModule.AddInitialStateOption(JoinArena);
            base.OnSubModuleLoad();
            Logger.Verbose("Bannerlord Coop Mod loaded");
        }

        private bool m_IsFirstTick = true;
        private bool missionLoaded = false;
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
                SandBoxSaveHelper.TryLoadSave(save, StartGameTavern, null);
            }
        }

        private static void StartClientInArena()
        {
            SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles(null);
            SaveGameFileInfo save = saveFiles.FirstOrDefault(s => ValidateModules(s.MetaData));

            if (save == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Unable to find a save without Mods. Create a fresh game and try again."));
            }
            else
            {
                SandBoxSaveHelper.TryLoadSave(save, StartGameArena, null);
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

        private static void StartGameTavern(LoadResult loadResult)
        {
            tavernManager = new MissionTestGameManager(loadResult);
            tavernManager.StartGameInTavern();
        }

        private static void StartGameArena(LoadResult loadResult)
        {
            arenaManager = new ArenaTestGameManager(loadResult);
            arenaManager.StartGameInArena();
        }
    }
}
