using Coop.Common;
using Coop.Game.Debug;
using HarmonyLib;
using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Coop.Game
{
    class Main : NoHarmony.NoHarmonyLoader
    {
        public static Main Instance;
        public Main()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);
            Instance = this;
            m_Updateables = new UpdateableList();
            m_Updateables.Add(CoopClient.Instance);
            m_Updateables.Add(GameLoopRunner.Instance);

            Persistence.Environment.Current = new GameEnvironment();
        }
        public override void NoHarmonyInit()
        {
            initLogger();
        }

        public override void NoHarmonyLoad()
        {
            AddBehavior<InitServerBehaviour>();
            AddBehavior<PlayerJoinedBehaviour>();

            var harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord");
            harmony.PatchAll();

            Module.CurrentModule.AddInitialStateOption(new InitialStateOption("Check server status.",
             new TextObject("Check server status.", null),
             9990,
             () =>
             {
                 if(CoopServer.Instance.Current == null)
                 {
                    Common.Log.Info("No server found.");
                 }
                 else
                 {
                    Common.Log.Info($"Server state: {CoopServer.Instance.Current.State}.");
                 }
             },
             false));
        }
        protected override void OnSubModuleUnloaded()
        {
            CoopServer.Instance.ShutDownServer();
            base.OnSubModuleUnloaded();
        }

        protected override void OnApplicationTick(float dt)
        {
            if(m_bFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                m_bFirstTick = false;
            }

            base.OnApplicationTick(dt);
            if (Input.DebugInput.IsControlDown() && Input.DebugInput.IsKeyDown(InputKey.Tilde))
            {
                Debug.Console.Toggle();
            }

            m_Updateables.UpdateAll(TimeSpan.FromMilliseconds(dt));
        }

        private void initLogger()
        {
            // NoHarmony
            this.Logging = true;

            // our own logger
            Common.Log.s_OnLogEntry = (Common.Log.ELevel eLevel, string sMessage) =>
            {
                using (StreamWriter sw = new StreamWriter("Coop.txt", true))
                    sw.WriteLine(sMessage);
                if (eLevel == Common.Log.ELevel.Info)
                {
                    InformationManager.DisplayMessage(new InformationMessage(sMessage, Color.White));
                }
                if (eLevel == Common.Log.ELevel.Warning)
                {
                    InformationManager.DisplayMessage(new InformationMessage(sMessage, Color.FromUint(0xFF0000)));
                }
                else if (eLevel == Common.Log.ELevel.Error)
                {
                    InformationManager.DisplayMessage(new InformationMessage(sMessage, Color.FromUint(0xFFFF00)));
                }
            };
        }
        private static void OnUnhandledException(Object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Common.Log.Error($"Unhandled exception: {ex} - {ex.Message}.");
        }

        private readonly UpdateableList m_Updateables;
        private bool m_bFirstTick = true;
    }
}
