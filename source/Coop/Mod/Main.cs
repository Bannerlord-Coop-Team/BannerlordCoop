using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using Coop.Lib.NoHarmony;
using Coop.Mod.Behaviour;
using Coop.Mod.DebugUtil;
using Coop.Mod.Patch;
using Coop.Mod.UI;
using CoopFramework;
using HarmonyLib;
using ModTestingFramework;
using Network.Infrastructure;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Screens;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using Logger = NLog.Logger;
using Module = TaleWorlds.MountAndBlade.Module;

namespace Coop.Mod
{
    internal class Main : NoHarmonyLoader
    {
        // Test Symbols
        public static readonly bool TESTING_ENABLED = false;

        public static readonly string LOAD_GAME = "MP";

        // -------------
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool m_IsFirstTick = true;

        #region MainMenuButtons
        public static InitialStateOption CoopCampaign =
            new InitialStateOption(
                "CoOp Campaign",
                new TextObject("Host Co-op Campaign"),
                9990,
                () =>
                {
                    string[] array = Utilities.GetFullCommandLineString().Split(' ');

                    if (Globals.DEBUG)
                    {
                        foreach (string argument in array)
                        {
                            if (argument.ToLower() == "/server")
                            {
                                //TODO add name to args
                                CoopServer.Instance.StartGame("MP");
                            }
                            else if (argument.ToLower() == "/client")
                            {
                                ServerConfiguration defaultConfiguration =
                                    new ServerConfiguration();
                                CoopClient.Instance.Connect(
                                    defaultConfiguration.NetworkConfiguration.LanAddress,
                                    defaultConfiguration.NetworkConfiguration.LanPort);
                            }
                        }
                    }
                    else
                    {
                        ScreenManager.PushScreen(
                            ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                new object[] { }));
                    }
                },
                () => { return false; });

        public static InitialStateOption JoinCoopGame =
            new InitialStateOption(
              "Join Coop Game",
              new TextObject("Join Co-op Campaign"),
              9991,
              JoinWindow,
              () => { return false; }
            );
        #endregion

        public Main()
        {
            Debug.DebugManager = Debugging.DebugManager;
            MBDebug.DisableLogging = false;

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

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop");
            CoopFramework.CoopFramework.InitPatches(Coop.IsCoopGameSession);

            // Skip startup splash screen
            if (Globals.DEBUG)
            {
                typeof(Module).GetField(
                                  "_splashScreenPlayed",
                                  BindingFlags.Instance | BindingFlags.NonPublic)
                              .SetValue(Module.CurrentModule, true);
            }

            if (TESTING_ENABLED)
            {
                TestingFramework suite = TestingFramework.Instance;
            }

            // Apply all patches via harmony
            harmony.PatchAll();

            #region ButtonAssignment
            CoopCampaign =
                new InitialStateOption(
                    "CoOp Campaign",
                    new TextObject("Host Co-op Campaign"),
                    9990,
                    () =>
                    {
                        string[] array = Utilities.GetFullCommandLineString().Split(' ');

                        if (Globals.DEBUG)
                        {
                            foreach (string argument in array)
                            {
                                if (argument.ToLower() == "/server")
                                {
                                    //TODO add name to args
                                    CoopServer.Instance.StartGame("MP");
                                }
                                else if (argument.ToLower() == "/client")
                                {
                                    ServerConfiguration defaultConfiguration =
                                        new ServerConfiguration();
                                    CoopClient.Instance.Connect(
                                        defaultConfiguration.NetworkConfiguration.LanAddress,
                                        defaultConfiguration.NetworkConfiguration.LanPort);
                                }
                            }
                        }
                        else
                        {
                            ScreenManager.PushScreen(
                                ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                    new object[] { }));
                        }
                    },
                    () => { return false; });

            JoinCoopGame =
                new InitialStateOption(
                  "Join Coop Game",
                  new TextObject("Join Co-op Campaign"),
                  9991,
                  JoinWindow,
                  () => { return false; }
                );

            Module.CurrentModule.AddInitialStateOption(CoopCampaign);

            Module.CurrentModule.AddInitialStateOption(JoinCoopGame);
            #endregion
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            CoopServer.Instance.ShutDownServer();
        }

        protected override void OnApplicationTick(float dt)
        {
            if (m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                m_IsFirstTick = false;
            }

            base.OnApplicationTick(dt);
            if (Input.IsKeyDown(InputKey.LeftControl) && Input.IsKeyDown(InputKey.Tilde))
            {
                CLICommands.ShowDebugUi(new List<string>());
                // DebugConsole.Toggle();
            }

            Updateables.UpdateAll(TimeSpan.FromSeconds(dt));
        }

        private void initLogger()
        {
            // NoHarmony
            Logging = true;

            // NLog
            Target.Register<MbLogTarget>("MbLog");
            Mod.Logging.Init(
                new Target[]
                {
                    new MbLogTarget
                    {
                        Layout = Layout.FromString("[${level:uppercase=true}] ${message}")
                    }
                });
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception) e.ExceptionObject;
            Logger.Fatal(ex, "Unhandled exception");
        }


        internal static bool DisableIntroVideo = true;

        internal static bool EnableTalkToOtherLordsInAnArmy = true;

        internal static bool RecordFirstChanceExceptions = true;

        internal static bool DontGroupThirdPartyMenuOptions = true;

        internal static bool QuartermasterIsClanWide = true;


        internal static void JoinWindow()
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopConnectionUI>());
        }
    }
}
