using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Coop.Common;
using Coop.Network;
using Coop.Mod.Behaviour;
using Coop.Mod.CLI;
using Coop.Mod.UI;
using Coop.Sync;
using HarmonyLib;
using MBMultiplayerCampaign;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using NoHarmony;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using Module = TaleWorlds.MountAndBlade.Module;
using TaleWorlds.Engine;

namespace Coop.Mod
{
    internal class Main : NoHarmonyLoader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static readonly bool DEBUG = true;
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
                            CLICommands.ConnectTo(new List<string> { "127.0.0.1", "4201" });
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
                DebugConsole.Toggle();
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
