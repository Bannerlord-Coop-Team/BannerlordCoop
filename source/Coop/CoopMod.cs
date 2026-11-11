using Common;
using Common.Logging;
using Coop.Core.Common.Configuration;
using Common.Serialization;
using Coop.Core;
using Coop.Core.Common.Session;
using Coop.Core.Diagnostics;
using Coop.CrashReporting;
using Coop.Lib.NoHarmony;
#if DEBUG
using Coop.LiveTesting;
#endif
using Coop.UI.LoadGameUI;
using GameInterface;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Handlers;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.Chat;
using GameInterface.Services.Locations;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.Tournaments.UI;
using GameInterface.Services.UI;
using GameInterface.Services.UI.BugReporting;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CrashReporting;
using GameInterface.Utils;
using HarmonyLib;
using Serilog;
using System;
#if DEBUG
using System.Collections.Generic;
#endif
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
#if DEBUG
using static TaleWorlds.Library.CommandLineFunctionality;
#endif
using Module = TaleWorlds.MountAndBlade.Module;

namespace Coop
{
    internal class CoopMod : NoHarmonyLoader
    {
        public static UpdateableList Updateables => UpdateablesHolder.List;

        // Kept out of CoopMod's type initializer, which runs before the constructor's check for a foreign Common.dll
        private static class UpdateablesHolder
        {
            public static readonly UpdateableList List = new UpdateableList();
        }

        public static CoopartiveMultiplayerExperience Coop;

        public static InitialStateOption CoopCampaign;

        public static InitialStateOption JoinCoopGame;

        private static ILogger Logger;

        private string activeLogFilePath;
        private string informationalVersion = "unknown";
        private CrashReportingConsentCoordinator crashReportingConsent;
        private BugReportConsentCoordinator bugReportConsent;
        private IBugReportOverlay bugReportOverlay;
        private UnsupportedModuleWarningHandler unsupportedModuleWarning;
        private bool startupModuleWarningReady;
        private bool automaticCrashReportsRequested;
        private DateTime nextWatchdogRetryUtc;

#if DEBUG
        private LiveTestControlServer liveTestControlServer;
        private bool isDeferredClientJoin;
        private bool isLiveTestRun;
#endif

        public CoopMod()
        {
            MBDebug.DisableLogging = false;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Runs before Coop code uses Common or 0Harmony, the runtime binds same-named files in the game folder instead of ours.
            // Created directly because the DI container doesn't exist yet.
            var shadowingFiles = new ShadowedAssemblyDetector().FindShadowingFiles(AppDomain.CurrentDomain.BaseDirectory);
            if (shadowingFiles.Count > 0)
            {
                var message = "Coop can't start because these files in the game folder replace Coop's own copies. " +
                    "They come from another mod or an old install, move them out and start the game again:\n" +
                    string.Join("\n", shadowingFiles.Select(file => $"{file} ({FileVersionInfo.GetVersionInfo(file).FileVersion})"));
                // Same type as the game's own module load error box (Module.HandleSubmoduleLoadError), 16 showed Retry and Cancel
                TaleWorlds.Library.Debug.ShowMessageBox(message, "Coop", 256u);
                throw new InvalidOperationException(message);
            }

            // Must be called here (constructor), not in NoHarmonyLoad().
            // Module.LoadSubModules() calls all submodule constructors first, then all
            // OnSubModuleLoad() in a second pass. GauntletUISubModule.OnSubModuleLoad()
            // triggers the font race — our patches must be registered before that runs.
            BootPatches.Apply();
        }

        private static string ClientServerModeMessage = "";

        private bool isServer = false;
        private bool isAutoConnect = false;
        private bool autoConnectArgumentsValid;
        private NetworkConfig autoConnectConfiguration;
        public override void NoHarmonyInit() 
        {
            AssemblyHellscape.CreateAssemblyBindingRedirects();
            ProtoBufSerializer.ConfigureRuntimeModel();

            var args = Environment.GetCommandLineArgs();
            
            if (args.Any(a => a.Equals("/server", StringComparison.OrdinalIgnoreCase)))
            {
                isServer = true;
            }
            else if (args.Any(a => a.Equals("/client", StringComparison.OrdinalIgnoreCase)))
            {
                isServer = false;
            }

            autoConnectArgumentsValid = ServerLaunchArguments.TryParseAutoConnect(
                args, out isAutoConnect, out autoConnectConfiguration);

            if (ServerLaunchArguments.TryParse(Environment.GetCommandLineArgs(), out var managedSaveName,
                out var ownerProcessId, out var serverPassword, out var serverVisibility))
            {
                ManagedServerConfig.SaveName = managedSaveName;
                ManagedServerConfig.OwnerProcessId = ownerProcessId;
            }
            ManagedServerConfig.Password = serverPassword;
            ManagedServerConfig.Visibility = serverVisibility;
            
            SetupLogging();
            var moduleInfoProvider = new TaleWorldsModuleInfoProvider();
            // The dedicated server sets this itself; without it the module validator refuses NavalDLC on either side.
            bool navalModuleEnabled = moduleInfoProvider.GetModuleInfos()
                .Any(module => ModInformation.IsNavalDlcModule(module.Id, module.IsDlc));
            string navalUnsupportedReason = navalModuleEnabled
                ? new NavalFeatureFlag().GetUnsupportedReason(System.IO.Path.GetDirectoryName(typeof(CoopMod).Assembly.Location))
                : null;
            if (navalUnsupportedReason != null)
                Logger.Warning("NavalDLC is enabled but {Reason}, so NavalDLC is treated as unsupported", navalUnsupportedReason);
            ModInformation.IsNavalEnabled = navalModuleEnabled && navalUnsupportedReason == null;
            StartupDiagnosticsSequence.Run(version =>
                {
                    informationalVersion = version;
                    CoopLogHeader(moduleInfoProvider);
                },
                version => InitializeCrashReporting());

            Logger.Verbose("Coop Mod Module Started");
            
            Updateables.Add(new FpsLogger());

#if DEBUG
            isDeferredClientJoin = args.Any(a =>
                                       a.Equals("/cooptestmanualjoin", StringComparison.OrdinalIgnoreCase)) &&
                                   LiveTestControlServer.IsEnabled(Environment.GetCommandLineArgs());
            isLiveTestRun = LiveTestControlServer.IsEnabled(Environment.GetCommandLineArgs());
#endif

            // Creates the handler during launch
            if (!isServer)
            {
                unsupportedModuleWarning = new UnsupportedModuleWarningHandler(
                    moduleInfoProvider,
                    new TaleWorldsModuleNameProvider(),
                    new CoopOptionsStore(),
                    exception => Logger.Warning(
                        exception,
                        "Unsupported module wwarning preference could not be saved"),
                    new ModuleValidator());
            }

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

            // Reuse one boot Harmony owner for categories that must be active before normal PatchAll.
            var bootHarmony = new Harmony("Coop.UILoading");
            bootHarmony.PatchCategory(
                typeof(IGameInterface).Assembly, GameInterface.GameInterface.HARMONY_UI_LOADING_CATEGORY);
            bootHarmony.PatchCategory(
                typeof(IGameInterface).Assembly,
                GameInterface.GameInterface.HARMONY_CONFIGURED_MINOR_FACTION_CATEGORY);

            GameThread.Instance.MarkGameThread();
        }

        // Never disposed, because it holds the claim on the canonical log for the whole process.
        private static ILogFileOpener logFileOpener;

        // Created in SetupLogging, not here, because CoopMod's type initializer runs before the check for a foreign Common.dll.
        private static ISessionSummarySink sessionSummary;

        // Opens this session's log file and configures the root logger, before anything logs.
        private void SetupLogging()
        {
            const long maxLogFileSizeBytes = 30L * 1024 * 1024;
            const long preservedLogBeginningBytes = 1L * 1024 * 1024;
            const long compactedLogFileSizeBytes = 28L * 1024 * 1024;
            var outputTemplate = "[({ProcessId}) {Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

            sessionSummary = new SessionSummarySink(LogManager.GetLogger<SessionSummarySink>());
            var configuration = LogManager.Configuration
                .Enrich.WithProcessId()
                .WriteTo.Sink(sessionSummary);
#if DEBUG
            configuration.MinimumLevel.Debug();
#else
            configuration.MinimumLevel.Information();
#endif
            logFileOpener = new LogFileOpener(
                Directory.GetCurrentDirectory(),
                isServer ? "server" : "client",
                System.Diagnostics.Process.GetCurrentProcess().Id);
            var logFile = logFileOpener.Open(path => configuration.WriteTo.Sink(new SizeLimitedFileSink(
                path,
                outputTemplate,
                maxLogFileSizeBytes,
                preservedLogBeginningBytes,
                compactedLogFileSizeBytes)));
            activeLogFilePath = logFile.OpenedPath;

            // The first event freezes the configuration, so this stays last.
            Logger = LogManager.GetLogger<CoopMod>();
            logFile.LogWarnings(Logger);
        }

        private void CoopLogHeader(IModuleInfoProvider moduleInfoProvider)
        {
            var modules = moduleInfoProvider.GetModuleInfos().ToArray();
            var native = modules.FirstOrDefault(m => m.IsOfficial && m.Id.Equals("Native",  StringComparison.OrdinalIgnoreCase));

            if (native.Id == null)
            {
                // ModuleInfo was not initialized (not found).
                native.Id = "Unknown";
                native.IsDlc = false;
                native.IsOfficial = false;
                native.Version = ApplicationVersion.Empty;
            }
            
            Logger.Information("========================================================");
            Logger.Information("Bannerlord Coop - {client}", isServer ? "Server" : "Client");
            Logger.Information("Game Version: {major}.{minor}.{revision}", native.Version.Major, native.Version.Minor, native.Version.Revision);
            Logger.Information("Coop Build {version}", informationalVersion);
            Logger.Information(
                "[Protobuf] MonoRuntime={MonoRuntime} AutoCompile={AutoCompile} StructFactoryWorkaround={StructFactoryWorkaround} CLRVersion={ClrVersion}",
                ProtoBufSerializer.IsMonoRuntime,
                ProtoBufSerializer.AutoCompileEnabled,
                ProtoBufSerializer.StructFactoryWorkaroundEnabled,
                Environment.Version);
            Logger.Information("Current modules:" );

            foreach (var module in modules)
            {
                string official = module.IsOfficial ? "Official" : "Unofficial";
                Logger.Information("{official} {version} {name}", official, module.Version.ToString(), module.Id);
            }
            
            Logger.Information("========================================================");
        }

        private void InitializeCrashReporting()
        {
            var role = isServer ? "server" : "client";
            CrashDiagnostics.Initialize(informationalVersion, role);
            CrashDiagnostics.SetPhase("module-initialized");

            crashReportingConsent = new CrashReportingConsentCoordinator(
                new CoopOptionsStore(),
                () =>
                {
                    automaticCrashReportsRequested = true;
                    TryApplyAutomaticCrashReports();
                },
                exception => Logger.Warning(exception, "Crash reporting preference could not be saved"));
            crashReportingConsent.ApplyStoredDecision();
            bugReportConsent = new BugReportConsentCoordinator(
                new CoopOptionsStore(),
                exception => Logger.Warning(exception, "Diagnostic bug-report log-sharing preference could not be saved"));
#if !DEBUG
            TryStartCrashReporter(role);
#endif
        }

        private void TryStartCrashReporter(string role)
        {
            try
            {
                // The reporter refuses to start without a log to attach.
                if (activeLogFilePath == null)
                {
                    Logger.Warning("Crash reporter was not started: this session has no log file");
                    return;
                }

                string moduleDirectory = System.IO.Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);
                string reporterPath = System.IO.Path.Combine(
                    moduleDirectory,
                    "Coop.CrashReporter.exe");
                if (!File.Exists(reporterPath))
                {
                    Logger.Warning("Crash reporter was not found at {Path}", reporterPath);
                    return;
                }

                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    string readyEventName =
                        "Local\\CoopCrashReporterReady_" +
                        currentProcess.Id.ToString(CultureInfo.InvariantCulture) +
                        "_" +
                        Guid.NewGuid().ToString("N");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = reporterPath,
                        Arguments = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} {1}",
                            currentProcess.Id,
                            currentProcess.StartTime.ToUniversalTime().Ticks),
                        WorkingDirectory = moduleDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };
                    startInfo.EnvironmentVariables["COOP_CRASH_LOG"] = activeLogFilePath;
                    startInfo.EnvironmentVariables["COOP_CRASH_ROLE"] = role;
                    startInfo.EnvironmentVariables["COOP_CRASH_BUILD"] = informationalVersion;
                    startInfo.EnvironmentVariables["COOP_CRASH_READY"] = readyEventName;

                    string gameBinariesDirectory = GameBinariesDirectory.Resolve();
                    if (gameBinariesDirectory == null)
                    {
                        Logger.Warning(
                            "Game binaries directory could not be resolved; crash reports will not list installed game files");
                    }
                    else
                    {
                        startInfo.EnvironmentVariables["COOP_CRASH_GAME_BINARIES"] = gameBinariesDirectory;
                    }

                    using (var ready = new EventWaitHandle(
                        false,
                        EventResetMode.AutoReset,
                        readyEventName))
                    {
                        Process reporterProcess = Process.Start(startInfo);
                        if (reporterProcess == null ||
                            !ready.WaitOne(TimeSpan.FromSeconds(2)))
                        {
                            Logger.Warning("Crash reporter did not confirm that it was ready");
                        }

                        reporterProcess?.Dispose();
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Warning(exception, "Crash reporter could not be started");
            }
        }

        private void TryApplyAutomaticCrashReports()
        {
            if (!automaticCrashReportsRequested) return;
            if (DateTime.UtcNow < nextWatchdogRetryUtc) return;

            nextWatchdogRetryUtc = DateTime.UtcNow.AddSeconds(1);
            try
            {
                if (!Watchdog.Attached()) return;

                Utilities.SetWatchdogAutoreport(true);
                automaticCrashReportsRequested = false;
                Logger.Information("Bannerlord automatic crash reports enabled by saved user consent");
            }
            catch (Exception exception)
            {
                nextWatchdogRetryUtc = DateTime.UtcNow.AddSeconds(30);
                Logger.Warning(exception, "Bannerlord automatic crash reports could not be enabled");
            }
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
            Coop = new CoopartiveMultiplayerExperience(
                isServer,
                CrashDiagnostics.SetPhase,
                activeLogFilePath);

#if DEBUG
            global::Coop.Core.Common.Commands.JoinDebugCommands.ConfigureClientSessionStarter(
                () => Coop.StartAsClient());
#endif

            Updateables.Add(GameThread.Instance);

#if DEBUG
            if (isAutoConnect && LiveTestControlServer.IsEnabled(Environment.GetCommandLineArgs()))
                TryStartLiveTestControl();
#endif

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
                        else if (!Coop.StartAsClient())
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                CoopartiveMultiplayerExperience.StartRefusedNotice));
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

#if DEBUG
        // Starts the live-test control server, which reads this session's log and can't start without one.
        private void TryStartLiveTestControl()
        {
            if (activeLogFilePath == null)
            {
                Logger.Warning("Live-test control was not started: this session has no log file");
                return;
            }

            liveTestControlServer = new LiveTestControlServer(
                isServer,
                activeLogFilePath,
                isDeferredClientJoin,
                () => autoConnectArgumentsValid && Coop.StartAsClient(autoConnectConfiguration));
            liveTestControlServer.Start();
        }
#endif

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            CrashDiagnostics.SetPhase("campaign-running");

            if (ContainerProvider.TryResolve<IGameInterface>(out var gameInterface))
                gameInterface.PatchGameStarted();

            if (ModInformation.IsClient && ContainerProvider.TryResolve<IChatService>(out var chatService))
                chatService.Initialize();
            if (ModInformation.IsClient && ContainerProvider.TryResolve<GameInterface.Services.Voice.IVoiceSpeakingOverlay>(out var voiceOverlay))
                voiceOverlay.Initialize();

            if (gameStarterObject is CampaignGameStarter campaignGameStarter)
            {
                if (ContainerProvider.TryResolve<ICoopModulePathResolver>(out var modulePathResolver))
                    campaignGameStarter.AddBehavior(new FixedTownNpcConversationBehavior(modulePathResolver));
                campaignGameStarter.AddBehavior(new PlayerPartyInteractionCampaignBehavior());
                campaignGameStarter.AddBehavior(new CoopTournamentCampaignBehavior());
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            if (isServer)
            {
                ManagedOptions.SetConfig(ManagedOptions.ManagedOptionsType.StopGameOnFocusLost, 0f);
            }

            CrashDiagnostics.SetPhase("main-menu");
            startupModuleWarningReady = true;
            InformationManager.DisplayMessage(new InformationMessage(ClientServerModeMessage));
        }

        public override void OnGameEnd(Game game)
        {
            CrashDiagnostics.SetPhase("ending-game");
            base.OnGameEnd(game);

            if (Coop.Running)
            {
                Coop.EndSession();
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            CrashDiagnostics.SetPhase("module-unloading");
            Coop?.Dispose();
#if DEBUG
            liveTestControlServer?.Dispose();
            liveTestControlServer = null;
#endif
            sessionSummary?.WriteSummary("exit");
            base.OnSubModuleUnloaded();
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

            var isAtMainMenu = GameStateManager.Current?.ActiveState is InitialState;
            TryApplyAutomaticCrashReports();
            TryShowUnsupportedModuleWarning(isAtMainMenu);
            TryShowCrashReportingConsent(isAtMainMenu);
            TryShowBugReportConsent(isAtMainMenu);
            TryInitializeBugReportOverlay();

            // Boot Steam services once the main menu is up, so a +connect_lobby launch resolves while joining is possible.
            if (!steamBootAttempted && isAtMainMenu)
            {
                steamBootAttempted = true;
#if DEBUG
                if (isLiveTestRun)
                {
                    Logger.Information("[LiveTest] Steam integration disabled for this scoped runtime");
                }
                else
#endif
                {
                    var steamPump = SteamIntegrationBoot.TryStartWithCallbackPump(
                        isServer, Utilities.GetFullCommandLineString());
                    // The standalone server has no game frame of its own to dispatch its game-server callbacks.
                    if (steamPump != null) Updateables.Add(steamPump);
                }
            }

            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);
            if (ModInformation.IsClient && Coop.Running &&
                ContainerProvider.TryResolve<GameInterface.Services.Voice.IVoiceClient>(out var voiceClient))
                voiceClient.Tick();

            TryManagedServerAutoStart();

            TryAutoConnect();
        }

        private void TryShowCrashReportingConsent(bool isAtMainMenu)
        {
            if (crashReportingConsent == null || isServer || isAutoConnect ||
                ManagedServerConfig.IsManagedServer)
            {
                return;
            }

            crashReportingConsent.TryShowPrompt(
                isAtMainMenu && !InformationManager.IsAnyInquiryActive(),
                inquiry => InformationManager.ShowInquiry(inquiry));
        }

        private void TryShowBugReportConsent(bool isAtMainMenu)
        {
            if (bugReportConsent == null || isServer || isAutoConnect ||
                ManagedServerConfig.IsManagedServer)
            {
                return;
            }

            bugReportConsent.TryShowPrompt(
                isAtMainMenu && !InformationManager.IsAnyInquiryActive(),
                inquiry => InformationManager.ShowInquiry(inquiry));
        }

        private void TryInitializeBugReportOverlay()
        {
            if (isServer || Game.Current == null ||
                !ContainerProvider.TryResolve(out IBugReportOverlay overlay) ||
                ReferenceEquals(bugReportOverlay, overlay))
            {
                return;
            }

            try
            {
                overlay.Initialize();
                bugReportOverlay = overlay;
                Logger.Debug("Initialized the bug-report overlay");
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Could not initialize the bug-report overlay");
            }
        }

        // Show the one-time unsupported module warning when the client startup UI is available.
        private void TryShowUnsupportedModuleWarning(bool isAtMainMenu)
        {
            if (unsupportedModuleWarning == null || !startupModuleWarningReady || isServer || isAutoConnect)
            {
                return;
            }
            
            unsupportedModuleWarning.TryShowPrompt(
                isAtMainMenu && !InformationManager.IsAnyInquiryActive(),
                inquiry => InformationManager.ShowInquiry(inquiry));
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
#if DEBUG
            if (!isServer && isDeferredClientJoin) return;
#endif

            if (isAutoConnect && !_autoStarted &&
                GameStateManager.Current?.ActiveState is InitialState)
            {
                _autoStarted = true;
                if (!autoConnectArgumentsValid)
                {
                    Logger.Error("[AutoConnect] Invalid endpoint or duplicate flag. Expected /autoconnect [host[:port]]; no connection attempted.");
                    return;
                }
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
                        bool started = Coop.StartAsClient(autoConnectConfiguration);
                        Logger.Information("[AutoConnect] StartAsClient() returned {Started}", started);
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
            if (Logger == null)
            {
                // Logging isn't set up yet, so keep the exception in the boot log instead of dropping it
                BootPatches.FileLog($"Unhandled exception before logging was set up: {ex}");
                return;
            }

            Logger.Fatal(ex, "Unhandled exception");
            Logger.Fatal(ex.StackTrace);
            sessionSummary?.WriteSummary("crash");
            // The file sink flushes every event; this sends Seq's buffered events, if a Seq server is listening.
            LogManager.CloseAndFlush(TimeSpan.FromSeconds(1));
        }

        internal static void JoinWindow()
        {
            CrashDiagnostics.SetPhase("join-menu");
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopConnectionUI>());
        }

        public override void OnAfterGameInitializationFinished(Game game, object starterObject)
        {
            base.OnAfterGameInitializationFinished(game, starterObject);
        }
    }

#if DEBUG
    /// <summary>
    /// Starts a deferred live-test client through the normal connection path.
    /// </summary>
    internal static class JoinFixtureCommands
    {
        [CommandLineArgumentFunction("start", "coop.debug.connection")]
        public static string Start(List<string> args)
        {
            if (args.Count != 0)
            {
                return "Usage: coop.debug.connection.start";
            }

            if (ModInformation.IsServer)
            {
                return "start must be run on a client.";
            }

            if (ContainerProvider.TryResolve<Common.LogicStates.ILogic>(out _))
            {
                return "Client co-op connection is already starting or running.";
            }

            if (!CoopMod.Coop.StartAsClient())
            {
                throw new InvalidOperationException("Client co-op connection start was refused.");
            }
            return "Client co-op connection started.";
        }
    }
#endif
}
