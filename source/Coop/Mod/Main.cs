using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common;
using Common.MessageBroker;
using Coop.Lib.NoHarmony;
using Coop.Mod.Behaviour;
using Coop.Mod.Client;
using Coop.Mod.Config;
using Coop.Mod.PacketHandlers;
using Coop.Mod.Patch;
using Coop.Mod.LogicStates.Client;
using Coop.Mod.LogicStates.Server;
using CoopFramework;
using HarmonyLib;
using NLog;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.ScreenSystem;
using Logger = NLog.Logger;
using Module = TaleWorlds.MountAndBlade.Module;
using Coop.Mod.Messages;
using Coop.Mod.GameInterfaces;
using Coop.Mod.GameInterfaces.Helpers;

namespace Coop.Mod
{
    internal class Main : NoHarmonyLoader
    {
        // Test Symbols
        public static readonly bool TESTING_ENABLED = true;

        public static readonly string LOAD_GAME = "MP";

        // -------------
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private static INetworkConfiguration networkConfiguration = new NetworkConfiguration();
        private static ICommunicator communicator;
        private static ICoopNetwork _network;
        private bool m_IsFirstTick = true;

        private bool _isDebugToggled = false;

        #region MainMenuButtons
        public static InitialStateOption CoopCampaign =
            new InitialStateOption(
                "CoOp Campaign",
                new TextObject("Host Co-op Campaign"),
                9990,
                () =>
                {
                    string[] array = Utilities.GetFullCommandLineString().Split(' ');

#if DEBUG
                    foreach (string argument in array)
                    {
                        
                    }

                    _network.Start();
#else
                    ScreenManager.PushScreen(
                        ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                            new object[] { }));
#endif
                },
                () => { return (false, new TextObject()); }
            );

        public static InitialStateOption JoinCoopGame =
            new InitialStateOption(
              "Join Coop Game",
              new TextObject("Join Co-op Campaign"),
              9991,
              JoinWindow,
              () => { return (false, new TextObject()); }
            );
        #endregion

        public Main()
        {
            CreateCommunicator();

            MBDebug.DisableLogging = false;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Updateables.Add(GameLoopRunner.Instance);
        }

        private static ICommunicator CreateCommunicator()
        {
            if(communicator == null)
            {
                IPacketManager packetManager = new PacketManager();
                IMessageBroker messageBroker = MessageBroker.Instance;

                IGameInterface gameInterface = CreateGameInterface();

                communicator = new CoopCommunicator(messageBroker, packetManager, gameInterface);
            }
            return communicator;
        }

        private static IGameInterface CreateGameInterface()
        {
            IExampleGameHelper exampleGameHelper = new ExampleGameHelper();

            return new GameInterface(exampleGameHelper);
        }

        private static string ClientServerModeMessage = ""; 

        public UpdateableList Updateables { get; } = new UpdateableList();

        public override void NoHarmonyInit()
        {
            // TODO init DI module
        }

        public override void NoHarmonyLoad()
        {
            AddBehavior<InitServerBehaviour>();
            AddBehavior<GameLoadedBehaviour>();

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop");
            // Apply all patches via harmony
            harmony.PatchAll();

            // Skip startup splash screen
#if DEBUG
            typeof(Module).GetField(
                                "_splashScreenPlayed",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(Module.CurrentModule, true);
#endif


            var args = Utilities.GetFullCommandLineString().Split(' ').ToList();
#if DEBUG
            bool isServer = false;
            if (args.Contains("/server"))
            {
                isServer = true;
            }
            else if (args.Contains("/client"))
            {
                isServer = false;
            }

#else
                        ScreenManager.PushScreen(
                            ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                new object[] { }));
#endif
            #region ButtonAssignment
            CoopCampaign =
                new InitialStateOption(
                    "CoOp Campaign",
                    new TextObject(isServer ? "Host Co-op Campaign" : "Join Co-op Campaign"),
                    9990,
                    () =>
                    {
#if DEBUG
                        string[] array = Utilities.GetFullCommandLineString().Split(' ');

                        if (isServer)
                        {
                            // TODO start network as server using config
                            IServerLogic context = new ServerLogic(_logger, communicator);
                            _network = new CoopServer(networkConfiguration, context);
                        }
                        else
                        {
                            // TODO start network as client using config
                            IClientLogic context = new ClientLogic(_logger, communicator);
                            _network = new CoopClient(networkConfiguration, context);
                        }

#else
                        ScreenManager.PushScreen(
                            ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                new object[] { }));
#endif
                        _network.Start();
                    },

                    () => { return (false, new TextObject()); }
                );
        
            JoinCoopGame =
                new InitialStateOption(
                  "Join Coop Game",
                  new TextObject("Join Co-op Campaign"),
                  9991,
                  JoinWindow,
              () => { return (false, new TextObject()); }
                );

            Module.CurrentModule.AddInitialStateOption(CoopCampaign);

            //Module.CurrentModule.AddInitialStateOption(JoinCoopGame);
            #endregion
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _network.Stop();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(ClientServerModeMessage));
        }

        //public Action<Game> OnGameInit;
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            //OnGameInit?.Invoke(game);
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
        }

        protected override void OnApplicationTick(float dt)
        {
            if (m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                m_IsFirstTick = false;
            }

            base.OnApplicationTick(dt);

            if (Input.IsKeyDown(InputKey.LeftControl) && Input.IsKeyDown(InputKey.Tilde) && this._isDebugToggled == false) {
                // TODO add back CLI
                this._isDebugToggled = true;
            } else if(Input.IsKeyReleased(InputKey.LeftControl) || Input.IsKeyReleased(InputKey.Tilde)) {
                this._isDebugToggled = false;
            }

            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.MakeUnion(SyncBufferManager.ProcessBufferedChanges).UpdateAll(frameTime);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            //Logger.Fatal(ex, "Unhandled exception");
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
