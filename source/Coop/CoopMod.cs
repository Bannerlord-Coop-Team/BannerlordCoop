using Common;
using Common.Logging;
using Coop.Core;
using Coop.Core.Common.Session;
using Coop.Lib.NoHarmony;
using Coop.UI.LoadGameUI;
using GameInterface;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.UI;
using GameInterface.Utils;
using HarmonyLib;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
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

            // GetFullCommandLineString splits on spaces, which would cut a quoted save
            // name apart; the managed-server arguments need real Windows arg parsing.
            if (ServerLaunchArguments.TryParse(Environment.GetCommandLineArgs(), out var managedSaveName,
                out var ownerProcessId, out var serverPassword, out var serverVisibility))
            {
                ManagedServerConfig.SaveName = managedSaveName;
                ManagedServerConfig.OwnerProcessId = ownerProcessId;
            }
            ManagedServerConfig.Password = serverPassword;
            ManagedServerConfig.Visibility = serverVisibility;

            SetupLogging();

            if (ManagedServerConfig.IsManagedServer)
            {
                Logger.Information("[ManagedServer] Spawned by process {OwnerProcessId} to host save '{SaveName}'",
                    ManagedServerConfig.OwnerProcessId, ManagedServerConfig.SaveName);
            }

            if (isAutoConnect)
            {
                // Launch arguments can include the hosted-server password; never write them to logs.
                Logger.Information("[AutoConnect] Launch arguments detected");
                Logger.Information("[AutoConnect] isServer={IsServer} isAutoConnect={IsAutoConnect}", isServer, isAutoConnect);
                EnsureSafeExitConfig();
            }

            // Boot-apply the loading-window patches so the keepalive guard exists before a host or join waits on PatchAll
            new Harmony("Coop.UILoading").PatchCategory(
                typeof(IGameInterface).Assembly, GameInterface.GameInterface.HARMONY_UI_LOADING_CATEGORY);

            GameThread.Instance.MarkGameThread();
        }

        // Held open for the whole process lifetime (never disposed) so the claim can't race with a second
        // instance's own attempt: a check-then-release probe would leave a window, between our own close and
        // Serilog's later open of the real log file, where a second process's identical probe could also
        // succeed. A sidecar lock file kept open under FileShare.None the entire time closes that window, and
        // is independent of whatever sharing mode Serilog itself uses to open the actual log file.
        private static FileStream logLockHandle;

        // True if filePath was free (no other live process holds its lock).
        private static bool TryClaimExclusive(string filePath)
        {
            try
            {
                logLockHandle = new FileStream(filePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        // Keep only the newest few process-id-suffixed logs. Each dual-client run mints one under a fresh
        // pid, so unlike the canonical Coop_{postfix}.log (deleted and recreated every startup) they would
        // otherwise pile up unbounded. The canonical file has no pid and isn't matched, so it's never touched.
        private static void PruneProcessSuffixedLogs(string filePostfix)
        {
            const int keep = 5;
            try
            {
                // The extension filter guards the .NET quirk where a 3-char pattern extension (.log) also
                // matches files whose extension merely starts with it, e.g. our own .log.lock sidecar files.
                var stale = Directory.GetFiles(Directory.GetCurrentDirectory(), $"Coop_{filePostfix}_*.log")
                    .Where(f => System.IO.Path.GetExtension(f).Equals(".log", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Skip(keep);
                foreach (var file in stale)
                {
                    try { File.Delete(file); }
                    catch (Exception) { /* still open by a live instance, or already gone — skip */ }
                }
            }
            catch (Exception)
            {
                // Best effort — log housekeeping must never break startup.
            }
        }

        private void SetupLogging()
        {
            var outputTemplate = "[({ProcessId}) {Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

            var filePostfix = isServer ? "server" : "client";
            var filePath = $"Coop_{filePostfix}.log";

            // File.Delete alone can't detect another live instance: it succeeds even on a file another
            // process still has open, as long as that handle allows shared delete (Serilog's file sink
            // does), so two same-install clients would silently keep fighting over one file. An exclusive
            // open is the only check that actually fails when someone else already has the file open.
            if (!TryClaimExclusive(filePath))
                filePath = $"Coop_{filePostfix}_{System.Diagnostics.Process.GetCurrentProcess().Id}.log";

            PruneProcessSuffixedLogs(filePostfix);

            try
            {
                File.Delete(filePath);
            }
            catch (Exception)
            {
                // Best effort delete
            }

            LogManager.Configuration
                .Enrich.WithProcessId()
                //.WriteTo.Debug(outputTemplate: outputTemplate) // Disabled: floods VS Output window causing frame hitching when debugger is attached
                .WriteTo.File(filePath, outputTemplate: outputTemplate)
#if DEBUG
                .MinimumLevel.Debug();
#else
                .MinimumLevel.Information();
#endif

            Logger = LogManager.GetLogger<CoopMod>();

            var informationalVersion = typeof(ModInformation).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "unknown";
            Logger.Information("BannerlordCoop build {Build}", informationalVersion);

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

            Updateables.Add(GameThread.Instance);


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
                    new TextObject(isServer ? "Host Co-op Sandbox" : "Join Co-op Sandbox"),
                    9990,
                    () =>
                    {
                        string[] array = Utilities.GetFullCommandLineString().Split(' ');

                        if (isServer)
                        {
                            Coop.StartAsServer(null, ManagedServerConfig.Password, ManagedServerConfig.Visibility);
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
                    new TextObject("Host Co-op Sandbox"),
                    9990,
                    () =>
                    {
                        ScreenManager.PushScreen(
                            ViewCreatorManager.CreateScreenView<CoopLoadScreen>(
                                new object[] { }));
                    },
                    () => { return (false, new TextObject("")); }
                );
#endif

            Module.CurrentModule.AddInitialStateOption(CoopCampaign);

#if !DEBUG
            JoinCoopGame =
                new InitialStateOption(
                  "Join Coop Game",
                  new TextObject("Join Co-op Sandbox"),
                  9991,
                  JoinWindow,
              () => { return (false, new TextObject("")); }
            );
            Module.CurrentModule.AddInitialStateOption(JoinCoopGame);
#endif
            #endregion
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (gameStarterObject is CampaignGameStarter campaignGameStarter)
                campaignGameStarter.AddBehavior(new PlayerPartyInteractionCampaignBehavior());
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
        private bool steamBootAttempted = false;
        protected override void OnApplicationTick(float dt)
        {
            if(m_IsFirstTick)
            {
                GameThread.Instance.MarkGameThread();

#if DEBUG
                WindowTitle.Apply(isServer);
#endif

                m_IsFirstTick = false;
            }

            // Boot Steam services once the main menu is up, so a +connect_lobby launch resolves while joining is possible.
            if (!steamBootAttempted && GameStateManager.Current?.ActiveState is InitialState)
            {
                steamBootAttempted = true;
                var steamPump = SteamIntegrationBoot.TryStartWithCallbackPump(
                    isServer, Utilities.GetFullCommandLineString());
                // The standalone server has no game frame of its own to dispatch its game-server callbacks.
                if (steamPump != null) Updateables.Add(steamPump);
            }

            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);

            TryManagedServerAutoStart();

#if DEBUG
            TryAutoConnect();
#endif
        }

        private bool _managedAutoStarted = false;
        private void TryManagedServerAutoStart()
        {
            // Keyed on the auto-load save, not the UI-spawned marker: a manually launched
            // /coopsave server also auto-loads without an owner-process id.
            if (!isServer || !ManagedServerConfig.HasAutoLoadSave || _managedAutoStarted) return;
            if (!(GameStateManager.Current?.ActiveState is InitialState)) return;

            _managedAutoStarted = true;
            Logger.Information("[ManagedServer] InitialState active — hosting save '{SaveName}'", ManagedServerConfig.SaveName);

            try
            {
                Coop.StartAsServer(ManagedServerConfig.SaveName, ManagedServerConfig.Password,
                    ManagedServerConfig.Visibility);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ManagedServer] Exception during auto-start");
            }
        }

        private void TryAutoConnect()
        {
            // The auto-load-save start path owns this process's startup.
            if (ManagedServerConfig.HasAutoLoadSave) return;

            if (isAutoConnect && !_autoStarted && GameStateManager.Current?.ActiveState is InitialState)
            {
                _autoStarted = true;
                try
                {
                    if (isServer)
                    {
                        Logger.Information("[AutoConnect] InitialState active — auto-starting as server...");
                        Coop.StartAsServer(null, ManagedServerConfig.Password, ManagedServerConfig.Visibility);
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
