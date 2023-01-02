using Common;
using Coop.Mod;
using Coop.Mod.Missions;
using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using NLog.Targets;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using NLog;
using Logger = NLog.Logger;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        
        protected override void OnSubModuleLoad()
        {
#if DEBUG
            var logTarget = new DebuggerTarget()
            {
	            Layout = "${date:format=HH\\:MM\\:ss} (${level}) [${logger}] : ${message} ${exception}",
            };
            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(logTarget, LogLevel.Trace);
#endif

			Updateables.Add(GameLoopRunner.Instance);

            JoinTavern = new InitialStateOption(
              "Join Online Tavern",
              new TextObject("Join Online Tavern"),
              9991,
              StartClientInTavern,
              () => (false, new TextObject()));

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            base.OnSubModuleLoad();
            Logger.Trace("Bannerlord Coop Mod loaded");
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
        };
        private static bool ValidateModules(MetaData metaData)
        {
            if(metaData == null) return false;

            var moduleNames = metaData.GetModules();
            return moduleNames.All(name => allowedModules.Contains(name));
        }

        private static void StartGame(LoadResult loadResult)
        {
            MissionTestGameManager manager = new MissionTestGameManager(loadResult);
            manager.StartGameInTavern();
        }
    }
}
