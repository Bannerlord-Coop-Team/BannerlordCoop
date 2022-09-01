using Common;
using Coop.Mod;
using Coop.Mod.Missions;
using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
    {
        public static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        
        protected override void OnSubModuleLoad()
        {
            Updateables.Add(GameLoopRunner.Instance);

            JoinTavern = new InitialStateOption(
              "Join Online Tavern",
              new TextObject("Join Online Tavern"),
              9991,
              () => { StartClientInTavern(); },
              () => { return (false, new TextObject()); }
            );

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            base.OnSubModuleLoad();
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
            "MissionTestMod"
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
