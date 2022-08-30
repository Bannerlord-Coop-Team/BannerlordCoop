using Common;
using Coop.Mod;
using Coop.Mod.Missions;
using HarmonyLib;
using SandBox;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
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
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");
        protected override void OnSubModuleLoad()
        {
            harmony.PatchAll();

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
            SaveGameFileInfo save = saveFiles.FirstOrDefault();

            if(save == null)
            {
                throw new InvalidOperationException("No saves available to load");
            }

            SandBoxSaveHelper.TryLoadSave(save, StartGame, null);
        }

        private static void StartGame(LoadResult loadResult)
        {
            MBGameManager manager = new MissionTestGameManager(loadResult);
            MBGameManager.StartNewGame(manager);
            MouseManager.ShowCursor(false);
        }
    }
}
