using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common;
using Coop.Mod.Behaviour;
using Coop.Mod.DebugUtil;
using Coop.Mod.UI;
using HarmonyLib;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using NoHarmony;
using Sync;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

using Logger = NLog.Logger;
using Module = TaleWorlds.MountAndBlade.Module;
using Coop.Mod.Patch;
using Network.Infrastructure;

namespace Coop.Mod
{
    internal class Main : NoHarmonyLoader
    {

        // Debug symbols
        public static readonly bool DEBUG = true;
        public static readonly string LOAD_GAME = "MP";
        // -------------
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool m_IsFirstTick = true;

        public Main()
        {
            TaleWorlds.Library.Debug.DebugManager = Debugging.DebugManager;
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
            IEnumerable<Type> patches =
                from t in Assembly.GetExecutingAssembly().GetTypes()
                where t.IsDefined(typeof(PatchAttribute))
                select t;
            foreach (Type patch in patches)
            {
                FieldWatcher.ApplyFieldWatcherPatches(harmony, patch);
            }

            harmony.PatchAll();

            Module.CurrentModule.AddInitialStateOption(new InitialStateOption("CoOp Campaign",
            new TextObject("Co-op Campaign", null),
            9990,
            () =>
            {
                string[] array = Utilities.GetFullCommandLineString().Split(new char[]
                {
                    ' '
                });

                

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
                            ServerConfiguration defaultConfiguration = new ServerConfiguration();
                            CoopClient.Instance.Connect(defaultConfiguration.LanAddress, defaultConfiguration.LanPort);
                        }
                    }
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("Hello World!"));
                    ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopLoadScreen>(new object[] { }));
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
            if (m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                m_IsFirstTick = false;
            }

            base.OnApplicationTick(dt);
            if (Input.DebugInput.IsControlDown() && Input.DebugInput.IsKeyDown(InputKey.Tilde))
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
            Common.Logging.Init(
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
    }
}
