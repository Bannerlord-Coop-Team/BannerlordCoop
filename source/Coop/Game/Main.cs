using System;
using System.IO;
using Coop.Common;
using Coop.Game.Behaviour;
using Coop.Game.CLI;
using HarmonyLib;
using NoHarmony;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Coop.Game
{
    internal class Main : NoHarmonyLoader
    {
        private bool m_IsFirstTick = true;

        public Main()
        {
            Instance = this;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Updateables.Add(CoopClient.Instance);
            Updateables.Add(GameLoopRunner.Instance);
        }

        public static Main Instance { get; private set; }
        public UpdateableList Updateables { get; } = new UpdateableList();

        public override void NoHarmonyInit()
        {
            initLogger();
        }

        public override void NoHarmonyLoad()
        {
            AddBehavior<InitServerBehaviour>();
            AddBehavior<GameLoadedBehaviour>();

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord");
            harmony.PatchAll();

            Module.CurrentModule.AddInitialStateOption(
                new InitialStateOption(
                    "Check server status.",
                    new TextObject("Check server status."),
                    9990,
                    () =>
                    {
                        Common.Log.Info(
                            CoopServer.Instance.Current == null ?
                                "No server found." :
                                $"Server state: {CoopServer.Instance.Current.State}.");
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
            if (m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                m_IsFirstTick = false;
            }

            base.OnApplicationTick(dt);
            if (Input.DebugInput.IsControlDown() && Input.DebugInput.IsKeyDown(InputKey.Tilde))
            {
                DebugConsole.Toggle();
            }

            Updateables.UpdateAll(TimeSpan.FromSeconds(dt));
        }

        private void initLogger()
        {
            // NoHarmony
            Logging = true;

            int iNrOfInstances = System.Diagnostics.Process.GetProcessesByName(
                                           System.IO.Path.GetFileNameWithoutExtension(
                                               System.Reflection.Assembly.GetEntryAssembly()
                                                     .Location))
                                       .Length;
            string sLogFileName = $"Coop_{iNrOfInstances - 1}.txt";
            // our own logger
            Common.Log.s_OnLogEntry = (eLevel, sMessage) =>
            {
                using (StreamWriter sw = new StreamWriter(sLogFileName, true))
                {
                    sw.WriteLine(sMessage);
                }

                if (eLevel == Common.Log.ELevel.Info)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(sMessage, Color.White));
                }

                if (eLevel == Common.Log.ELevel.Warning)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(sMessage, Color.FromUint(0xFF0000)));
                }
                else if (eLevel == Common.Log.ELevel.Error)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(sMessage, Color.FromUint(0xFFFF00)));
                }
            };
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception) e.ExceptionObject;
            Common.Log.Error($"Unhandled exception: {ex} - {ex.Message}.");
        }
    }
}
