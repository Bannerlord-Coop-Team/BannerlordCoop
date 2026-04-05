using Common;
using Common.Logging;
using Coop.Core;
using Coop.Lib.NoHarmony;
using GameInterface.Services.UI;
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

        public static CoopartiveMultiplayerExperience Coop;

        public static InitialStateOption CoopCampaign;

        public static InitialStateOption JoinCoopGame;

        private static ILogger Logger;

        public CoopMod()
        {
            MBDebug.DisableLogging = false;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Must be called here (constructor), not in NoHarmonyLoad().
            // Module.LoadSubModules() calls all submodule constructors first, then all
            // OnSubModuleLoad() in a second pass. GauntletUISubModule.OnSubModuleLoad()
            // triggers the font race — our patches must be registered before that runs.
            BootPatches.Apply();
        }

        private static string ClientServerModeMessage = "";

        private bool isServer = false;
        private bool isAutoConnect = false;
        public override void NoHarmonyInit() 
        {
            AssemblyHellscape.CreateAssemblyBindingRedirects();

            var fullCommandLine = Utilities.GetFullCommandLineString();
            var args = fullCommandLine.Split(' ').ToList();
            
            if (args.Any(a => a.Equals("/server", StringComparison.OrdinalIgnoreCase)))
            {
                isServer = true;
            }
            else if (args.Any(a => a.Equals("/client", StringComparison.OrdinalIgnoreCase)))
            {
                isServer = false;
            }

            isAutoConnect = args.Any(a => a.Equals("/autoconnect", StringComparison.OrdinalIgnoreCase));

            SetupLogging();

            if (isAutoConnect)
            {
                Logger.Information("[AutoConnect] Full command line: {CommandLine}", fullCommandLine);
                Logger.Information("[AutoConnect] isServer={IsServer} isAutoConnect={IsAutoConnect}", isServer, isAutoConnect);
                EnsureSafeExitConfig();
            }

            GameLoopRunner.Instance.SetGameLoopThread();
        }

        private void SetupLogging()
        {
            var outputTemplate = "[({ProcessId}) {Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

            var filePostfix = isServer ? "server" : "client";
            var filePath = $"Coop_{filePostfix}.log";

            try
            {
                // Clear old filepath
                File.Delete(filePath);
            }
            catch (IOException) { }

            LogManager.Configuration
                .Enrich.WithProcessId()
                .WriteTo.Debug(outputTemplate: outputTemplate)
                .WriteTo.File(filePath, outputTemplate: outputTemplate)
                .MinimumLevel.Verbose();

            Logger = LogManager.GetLogger<CoopMod>();
            Logger.Verbose("Coop Mod Module Started");
        }

        /// <summary>
        /// Sets safely_exited=1 in engine_config.txt and marks it read-only so Bannerlord never
        /// shows the safe-mode recovery popup when using DebugAutoConnect (both processes are killed
        /// hard by the debugger, so safely_exited is never written on normal shutdown).
        /// </summary>
        private void EnsureSafeExitConfig()
        {
            try
            {
                var configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Configs",
                    "engine_config.txt");

                if (!File.Exists(configPath))
                {
                    Logger.Warning("[AutoConnect] engine_config.txt not found at {Path} — safe-mode popup suppression skipped", configPath);
                    return;
                }

                // Set safely_exited=1 and lock the file read-only.
                // Both processes are killed hard by the debugger so Bannerlord never gets to write
                // safely_exited=0 on shutdown — without this the safe-mode popup appears every run.
                File.SetAttributes(configPath, FileAttributes.Normal);

                var lines = File.ReadAllLines(configPath);
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("safely_exited", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "safely_exited  = 1";
                        found = true;
                        break;
                    }
                }

                if (!found)
                    lines = lines.Concat(new[] { "safely_exited  = 1" }).ToArray();

                File.WriteAllLines(configPath, lines);
                File.SetAttributes(configPath, FileAttributes.ReadOnly);

                Logger.Information("[AutoConnect] engine_config.txt patched: safely_exited=1 and set read-only");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[AutoConnect] Failed to patch engine_config.txt — safe-mode popup may still appear");
            }
        }

        public override void NoHarmonyLoad()
        {
            Coop = new CoopartiveMultiplayerExperience();

            Updateables.Add(GameLoopRunner.Instance);


            // Skip startup splash screen
#if DEBUG
            typeof(Module).GetField(
                                "_splashScreenPlayed",
                                BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(Module.CurrentModule, true);
#endif
            #region ButtonAssignment

#if DEBUG
            CoopCampaign = new InitialStateOption(
                    "CoOp Campaign",
                    new TextObject(isServer ? "Host Co-op Campaign" : "Join Co-op Campaign"),
                    9990,
                    () =>
                    {
                        string[] array = Utilities.GetFullCommandLineString().Split(' ');

                        if (isServer)
                        {
                            Coop.StartAsServer();
                        }
                        else
                        {
                            Coop.StartAsClient();
                        }
                    },
                    () => { return (false, new TextObject("")); }
                );
#else
            CoopCampaign = new InitialStateOption(
                    "CoOp Campaign",
                    new TextObject("Host Co-op Campaign"),
                    9990,
                    () =>
                    {
                        ScreenManager.PushScreen(
                            ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                new object[] { }));
                    },
                    () => { return (false, new TextObject()); }
                );
#endif

            Module.CurrentModule.AddInitialStateOption(CoopCampaign);

#if !DEBUG
            JoinCoopGame =
                new InitialStateOption(
                  "Join Coop Game",
                  new TextObject("Join Co-op Campaign"),
                  9991,
                  JoinWindow,
              () => { return (false, new TextObject()); }
            );
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

            if (Coop.Running)
            {
                Coop.Dispose();
            }
        }

        private bool m_IsFirstTick = true;
        private bool _autoStarted = false;
        protected override void OnApplicationTick(float dt)
        {
            if(m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();
                
                m_IsFirstTick = false;
            }    
            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);

#if DEBUG
            TryAutoConnect();
#endif
        }

        private void TryAutoConnect()
        {
            if (isAutoConnect && !_autoStarted && GameStateManager.Current?.ActiveState is InitialState)
            {
                _autoStarted = true;
                try
                {
                    if (isServer)
                    {
                        Logger.Information("[AutoConnect] InitialState active — auto-starting as server...");
                        Coop.StartAsServer();
                        Logger.Information("[AutoConnect] StartAsServer() completed");
                    }
                    else
                    {
                        Logger.Information("[AutoConnect] InitialState active — auto-starting as client...");
                        Coop.StartAsClient();
                        Logger.Information("[AutoConnect] StartAsClient() completed");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[AutoConnect] Exception during auto-start");
                }
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Logger?.Fatal(ex, "Unhandled exception");
            Logger?.Fatal(ex.StackTrace);
            Serilog.Log.CloseAndFlush();
        }

        internal static void JoinWindow()
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopConnectionUI>());
        }

        public override void OnAfterGameInitializationFinished(Game game, object starterObject)
        {
            base.OnAfterGameInitializationFinished(game, starterObject);
        }
    }
}
