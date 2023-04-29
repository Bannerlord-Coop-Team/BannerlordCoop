using Common;
using Common.Logging;
using Coop.Core;
using Coop.Lib.NoHarmony;
using Coop.UI;
using HarmonyLib;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
using Module = TaleWorlds.MountAndBlade.Module;

namespace Coop
{
    internal class CoopMod : NoHarmonyLoader
    {
        public static UpdateableList Updateables { get; } = new UpdateableList();

        public static CoopartiveMultiplayerExperience Coop = new CoopartiveMultiplayerExperience();

        public static InitialStateOption CoopCampaign;

        public static InitialStateOption JoinCoopGame;

        private static ILogger Logger;

        public CoopMod()
        {
            MBDebug.DisableLogging = false;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private static string ClientServerModeMessage = "";

        private bool isServer = false;
        public override void NoHarmonyInit() 
        {
            AssemblyHellscape.CreateAssemblyBindingRedirects();

            var args = Utilities.GetFullCommandLineString().Split(' ').ToList();
            
            if (args.Contains("/server"))
            {
                isServer = true;
            }
            else if (args.Contains("/client"))
            {
                isServer = false;
            }

            SetupLogging();

            GameLoopRunner.Instance.SetGameLoopThread();
        }

        private void SetupLogging()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                var outputTemplate = "[({ProcessId}) {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

                var filePostfix = isServer ? "server" : "client";
                var filePath = $"Coop_{filePostfix}.log";

                try
                {
                    // Clear old filepath
                    File.Delete(filePath);
                } 
                catch(IOException) { }

                LogManager.Configuration
                    .Enrich.WithProcessId()
                    .WriteTo.Debug(outputTemplate: outputTemplate)
                    .WriteTo.File(filePath, outputTemplate: outputTemplate)
                    .MinimumLevel.Verbose();
            }

            Logger = LogManager.GetLogger<CoopMod>();
            Logger.Verbose("Coop Mod Module Started");
        }

        

        public override void NoHarmonyLoad()
        {
            Updateables.Add(GameLoopRunner.Instance);
            Updateables.Add(Coop);

            Harmony harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord.Coop");
            // Apply all patches via harmony
            harmony.PatchAll();

            // Skip startup splash screen
#if DEBUG
            typeof(Module).GetField(
                                "_splashScreenPlayed",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(Module.CurrentModule, true);
#else
            //ScreenManager.PushScreen(
            //    ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
            //        new object[] { }));
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
                            Coop.StartAsServer();
                        }
                        else
                        {
                            Coop.StartAsClient();
                        }
#else
                        //ScreenManager.PushScreen(
                        //    ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                        //        new object[] { }));
#endif
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

#if !DEBUG
            Module.CurrentModule.AddInitialStateOption(JoinCoopGame);
#endif
            #endregion
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(ClientServerModeMessage));
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
        }

        private bool m_IsFirstTick = true;
        protected override void OnApplicationTick(float dt)
        {
            if(m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                
                m_IsFirstTick = false;
            }    
            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Logger.Fatal(ex, "Unhandled exception");
            Logger.Fatal(ex.StackTrace);
            Serilog.Log.CloseAndFlush();
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
