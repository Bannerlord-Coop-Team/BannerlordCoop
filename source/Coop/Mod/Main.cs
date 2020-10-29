using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common;
using Coop.Lib.NoHarmony;
using Coop.Mod.Behaviour;
using Coop.Mod.BIT;
using Coop.Mod.DebugUtil;
using Coop.Mod.Managers;
using Coop.Mod.Patch;
using Coop.Mod.UI;
using HarmonyLib;
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
        // Debug symbols
        public static readonly bool DEBUG = true;
        // Test Symbols
        public static readonly bool BIT_ENABLED = true;

        public static readonly string LOAD_GAME = "MP";

        // -------------
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool m_IsFirstTick = true;

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

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord");
            IEnumerable<MethodInfo> patchInitializers =
                from t in Assembly.GetExecutingAssembly().GetTypes()
                from m in t.GetMethods()
                where m.IsDefined(typeof(PatchInitializerAttribute))
                select m;
            foreach (MethodInfo initializer in patchInitializers)
            {
                if (!initializer.IsStatic)
                {
                    throw new Exception("Invalid [PatchInitializer]. Has to be static.");
                }
                
                Logger.Info("Init patch {}", initializer.DeclaringType);
                initializer.Invoke(null, null);
            }

            // Skip startup splash screen
            if (DEBUG)
            {
                typeof(Module).GetField(
                                  "_splashScreenPlayed",
                                  BindingFlags.Instance | BindingFlags.NonPublic)
                              .SetValue(Module.CurrentModule, true);
            }

            if (BIT_ENABLED)
            {
                BITSuite suite = BITSuite.Instance;
            }

            // Apply all patches via harmony
            harmony.PatchAll();

            Module.CurrentModule.AddInitialStateOption(
                new InitialStateOption(
                    "CoOp Campaign",
                    new TextObject("Host Co-op Campaign"),
                    9990,
                    () =>
                    {
                        string[] array = Utilities.GetFullCommandLineString().Split(' ');

                        if (DEBUG)
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
                            InformationManager.DisplayMessage(
                                new InformationMessage("Hello World!"));
                            ScreenManager.PushScreen(
                                ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                    new object[] { }));
                        }
                    },
                    false));

            Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
              "Join Coop Game",
              new TextObject("Join Co-op Campaign"),
              9991,
              JoinWindow,
              false
            ));

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


        internal void JoinWindow()
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopConnectionUI>());
        }
    }
}
