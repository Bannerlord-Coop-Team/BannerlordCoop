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
        private readonly UpdateableList m_Updateables;
        private bool m_IsFirstTick = true;

        public Main()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            m_Updateables = new UpdateableList();
            m_Updateables.Add(CoopClient.Instance);
            m_Updateables.Add(GameLoopRunner.Instance);
        }

        public override void NoHarmonyInit()
        {
            initLogger();
        }

        public override void NoHarmonyLoad()
        {
            AddBehavior<InitServerBehaviour>();
            AddBehavior<PlayerJoinedBehaviour>();

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
                                "No server found."
                                : $"Server state: {CoopServer.Instance.Current.State}.");
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

            m_Updateables.UpdateAll(TimeSpan.FromMilliseconds(dt));
        }

        private void initLogger()
        {
            // NoHarmony
            Logging = true;

            // our own logger
            Common.Log.s_OnLogEntry = (eLevel, sMessage) =>
            {
                using (StreamWriter sw = new StreamWriter("Coop.txt", true))
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
