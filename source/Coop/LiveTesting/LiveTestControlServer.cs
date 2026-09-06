#if DEBUG
using Common;
using Common.LiveTesting;
using Common.Logging;
using Common.LogicStates;
using Coop.Core.Client;
using Coop.Core.Common.Commands;
using Coop.Core.Server;
using GameInterface;
using GameInterface.Services.LiveTesting;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Coop.LiveTesting
{
    internal sealed class LiveTestControlServer : IDisposable
    {
        private const string EndpointDirectoryName = "BannerlordCoop.LiveTest.v1";
        private const int MaximumScreenshotObservations = 120;
        private static readonly TimeSpan ScreenshotCaptureTimeout = TimeSpan.FromMinutes(1);

        private static readonly ILogger Logger;

        private readonly string logFilePath;
        private readonly bool isServer;
        private readonly bool deferredClientJoinEnabled;
        private readonly Func<bool> startAsClient;
        private readonly LiveTestProcessInfo processInfo;
        private readonly DateTime processStartedUtc;
        private readonly NamedPipeLiveTestServer pipeServer;
        private readonly IBmpScreenshotInspector screenshotInspector;
        private readonly object screenshotGate = new object();
        private readonly Dictionary<string, ScreenshotCaptureState> screenshotCaptures =
            new Dictionary<string, ScreenshotCaptureState>(StringComparer.Ordinal);
        private readonly HashSet<string> screenshotPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string endpointDirectory;
        private readonly string endpointRegistrationPath;
        private int shutdownScheduled;
        private int deferredClientJoinAttempted;
        private bool trackedRenderingEnabled = true;
        private bool renderStateTrackable = true;
        private int renderToggleCount;
        
        static LiveTestControlServer()
        {
            Logger = LogManager.GetLogger<LiveTestControlServer>();
        }

        public LiveTestControlServer(
            bool isServer,
            string logFilePath,
            bool deferredClientJoinEnabled,
            Func<bool> startAsClient)
        {
            if (string.IsNullOrWhiteSpace(logFilePath)) throw new ArgumentException("A log file path is required.", nameof(logFilePath));
            if (startAsClient == null) throw new ArgumentNullException(nameof(startAsClient));

            this.isServer = isServer;
            this.logFilePath = logFilePath;
            this.deferredClientJoinEnabled = deferredClientJoinEnabled;
            this.startAsClient = startAsClient;
            screenshotInspector = new BmpScreenshotInspector();

            int processId;
            using (Process process = Process.GetCurrentProcess())
            {
                processId = process.Id;
                processStartedUtc = process.StartTime.ToUniversalTime();
            }
            string[] arguments = Environment.GetCommandLineArgs();
            processInfo = new LiveTestProcessInfo
            {
                Pid = processId,
                ProcessStartedUtc = processStartedUtc,
                Role = isServer ? "server" : "client",
                PlatformId = ReadArgument(arguments, "/platformId"),
                RunToken = NormalizeRunToken(ReadArgument(arguments, "/cooptestrun")),
            };
            if (processInfo.RunToken == null)
                throw new InvalidOperationException("A valid /cooptestrun token is required for live testing.");

            string pipeName = LiveTestProtocol.GetPipeName(processId);
            pipeServer = new NamedPipeLiveTestServer(pipeName, GetProcessInfo, Handle);
            endpointDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                EndpointDirectoryName,
                processInfo.RunToken);
            endpointRegistrationPath = System.IO.Path.Combine(
                endpointDirectory,
                processId.ToString(CultureInfo.InvariantCulture) + ".json");
        }

        public static bool IsEnabled(string[] arguments) =>
            NormalizeRunToken(ReadArgument(arguments, "/cooptestrun")) != null;

        public void Start()
        {
            pipeServer.Start();
            try
            {
                WriteEndpointRegistration();
            }
            catch
            {
                pipeServer.Dispose();
                throw;
            }
            Logger.Information(
                "[LiveTest] Listening on {PipeName} as {Role} (platform {PlatformId}, run {RunToken})",
                LiveTestProtocol.GetPipeName(processInfo.Pid),
                processInfo.Role,
                processInfo.PlatformId ?? "none",
                processInfo.RunToken ?? "unscoped");
        }

        public void Dispose()
        {
            DeleteEndpointRegistration();
            pipeServer.Dispose();
        }

        private LiveTestResponse Handle(LiveTestRequest request)
        {
            switch (request.Method)
            {
                case "status":
                    return ExecuteOnGameThread(
                        request,
                        () => CreateStatusResponse(request.Id),
                        false);
                case "command-catalog":
                    return HandleCommandCatalog(request);
                case "command":
                    return HandleCommand(request);
                case "screenshot":
                    return HandleScreenshot(request);
                case "screenshot-status":
                    return HandleScreenshotStatus(request);
                case "render-status":
                    return HandleRenderStatus(request);
                case "render-toggle":
                    return HandleRenderToggle(request);
                case "join":
                    return HandleDeferredClientJoin(request);
                case "shutdown":
                    return HandleShutdown(request);
                default:
                    return Failure(
                        request.Id,
                        "method_not_found",
                        $"Unknown live-test method '{request.Method}'.",
                        false);
            }
        }

        private LiveTestResponse HandleCommandCatalog(LiveTestRequest request)
        {
            return ExecuteOnGameThread(request, () =>
            {
                if (!ContainerProvider.TryResolve<ILiveTestCommandDispatcher>(out var dispatcher))
                {
                    dispatcher = new LiveTestCommandDispatcher();
                }

                return Success(request.Id, new
                {
                    commands = dispatcher.GetCommandNames(),
                });
            }, false);
        }

        private LiveTestResponse HandleCommand(LiveTestRequest request)
        {
            if (!TryReadCommand(request.Parameters, out var command, out var arguments, out var error))
            {
                return Failure(request.Id, "invalid_parameters", error, false);
            }

            return ExecuteOnGameThread(request, () =>
            {
                if (!ContainerProvider.TryResolve<ILiveTestCommandDispatcher>(out var dispatcher))
                {
                    string output = null;
                    if (string.Equals(command, "coop.debug.connection.start", StringComparison.Ordinal))
                    {
                        output = Coop.JoinFixtureCommands.Start(arguments);
                    }
                    else if (string.Equals(command, "coop.debug.connection.reconnect", StringComparison.Ordinal))
                    {
                        output = JoinDebugCommands.Reconnect(arguments);
                    }

                    if (output != null)
                    {
                        bool hasFallbackStructuredResult = TryParseStructuredResult(
                            output,
                            out var fallbackStructuredResult);
                        return Success(request.Id, new
                        {
                            name = command,
                            arguments,
                            found = true,
                            output,
                            hasStructuredResult = hasFallbackStructuredResult,
                            structuredResult = fallbackStructuredResult,
                        });
                    }

                    return Failure(
                        request.Id,
                        "session_not_ready",
                        "The co-op session command dispatcher is not available yet.",
                        false);
                }

                LiveTestCommandResult result = dispatcher.Execute(command, arguments);
                if (!result.Found)
                {
                    return Failure(request.Id, "command_not_found", result.Output, false);
                }

                bool hasStructuredResult = TryParseStructuredResult(
                    result.Output,
                    out var structuredResult);
                return Success(request.Id, new
                {
                    name = command,
                    arguments,
                    found = true,
                    output = result.Output,
                    hasStructuredResult,
                    structuredResult,
                });
            }, true);
        }

        private LiveTestResponse HandleScreenshot(LiveTestRequest request)
        {
            if (!TryReadString(request.Parameters, "path", out var requestedPath) ||
                string.IsNullOrWhiteSpace(requestedPath) ||
                !System.IO.Path.IsPathRooted(requestedPath))
            {
                return Failure(
                    request.Id,
                    "invalid_parameters",
                    "Screenshot path must be an absolute Windows path.",
                    false);
            }

            string screenshotPath;
            try
            {
                screenshotPath = System.IO.Path.GetFullPath(requestedPath);
            }
            catch (Exception exception)
            {
                return Failure(request.Id, "invalid_parameters", exception.Message, false);
            }

            string captureId = Guid.NewGuid().ToString("N");

            return ExecuteOnGameThread(request, () =>
            {
                string directory = System.IO.Path.GetDirectoryName(screenshotPath);
                if (string.IsNullOrEmpty(directory))
                {
                    return Failure(
                        request.Id,
                        "invalid_parameters",
                        "Screenshot path has no parent directory.",
                        false);
                }

                var capture = new ScreenshotCaptureState(
                    captureId,
                    screenshotPath,
                    DateTime.UtcNow,
                    Utilities.EngineFrameNo,
                    MaximumScreenshotObservations,
                    ScreenshotCaptureTimeout);
                lock (screenshotGate)
                {
                    if (!screenshotPaths.Add(screenshotPath))
                    {
                        return Failure(
                            request.Id,
                            "screenshot_path_reused",
                            $"Screenshot path '{screenshotPath}' has already been used by this process.",
                            false);
                    }

                    screenshotCaptures.Add(captureId, capture);
                }

                try
                {
                    Directory.CreateDirectory(directory);
                    if (File.Exists(screenshotPath))
                    {
                        File.Delete(screenshotPath);
                    }

                    Utilities.TakeScreenshot(screenshotPath);
                }
                catch
                {
                    lock (screenshotGate)
                    {
                        screenshotCaptures.Remove(captureId);
                        screenshotPaths.Remove(screenshotPath);
                    }
                    throw;
                }

                return Success(request.Id, new
                {
                    captureId,
                    path = screenshotPath,
                    captureRequested = true,
                    captureRequestedUtc = capture.CaptureRequestedUtc,
                    captureRequestEngineFrame = capture.CaptureRequestEngineFrame,
                });
            }, true);
        }

        private LiveTestResponse HandleScreenshotStatus(LiveTestRequest request)
        {
            if (!TryReadString(request.Parameters, "captureId", out var captureId) ||
                captureId.Length != 32 ||
                !captureId.All(IsLowerHexadecimal))
            {
                return Failure(
                    request.Id,
                    "invalid_parameters",
                    "Screenshot status requires a 32-character lowercase hexadecimal capture id.",
                    false);
            }

            ScreenshotCaptureState capture;
            lock (screenshotGate)
            {
                if (!screenshotCaptures.TryGetValue(captureId, out capture))
                {
                    return Failure(
                        request.Id,
                        "capture_not_found",
                        $"Screenshot capture '{captureId}' was not found.",
                        false);
                }
            }

            if (!TryReadEngineObservation(
                request,
                out var observationUtc,
                out var observationEngineFrame,
                out var observationFailure))
            {
                return observationFailure;
            }

            lock (capture.Gate)
            {
                BmpScreenshotObservation observation = screenshotInspector.ObserveFile(capture.Path);
                ScreenshotCaptureAdvanceResult advance = capture.Advance(
                    observationUtc,
                    observationEngineFrame,
                    observation,
                    screenshotInspector);
                if (advance.Status == ScreenshotCaptureStatus.TimedOut)
                {
                    return Failure(
                        request.Id,
                        "screenshot_timeout",
                        $"Screenshot capture '{captureId}' did not stabilize within {capture.MaximumObservations} observations or {capture.CaptureTimeout.TotalSeconds:0} seconds.",
                        false);
                }

                if (advance.Status == ScreenshotCaptureStatus.QualityRejected)
                {
                    return CreateScreenshotQualityFailure(request.Id, capture);
                }

                return CreateScreenshotStatusResponse(request.Id, capture, advance.Stable);
            }
        }

        private LiveTestResponse CreateScreenshotQualityFailure(
            string requestId,
            ScreenshotCaptureState capture)
        {
            return Failure(
                requestId,
                "screenshot_quality_rejected",
                $"Screenshot capture '{capture.CaptureId}' was rejected as {capture.Evidence.QualityVerdict}: {capture.Evidence.QualityReason}",
                false);
        }

        private LiveTestResponse CreateScreenshotStatusResponse(
            string requestId,
            ScreenshotCaptureState capture,
            bool stable)
        {
            BmpScreenshotObservation observation = capture.LastObservation ?? BmpScreenshotObservation.Missing;
            BmpScreenshotEvidence evidence = capture.Evidence;
            return Success(requestId, new
            {
                captureId = capture.CaptureId,
                path = capture.Path,
                exists = observation.Exists,
                isBmp = observation.HeaderValid,
                headerValid = observation.HeaderValid,
                stable,
                complete = capture.Complete,
                length = observation.Length,
                declaredLength = observation.DeclaredLength,
                lengthMatchesHeader = observation.LengthMatchesHeader,
                width = evidence?.Width ?? observation.Width,
                height = evidence?.Height ?? observation.Height,
                bitsPerPixel = evidence?.BitsPerPixel ?? observation.BitsPerPixel,
                lastWriteUtc = observation.LastWriteUtc,
                captureRequestedUtc = capture.CaptureRequestedUtc,
                captureRequestEngineFrame = capture.CaptureRequestEngineFrame,
                observationUtc = capture.LastObservationUtc,
                observationEngineFrame = capture.LastObservationEngineFrame,
                observationCount = capture.ObservationCount,
                maximumObservations = capture.MaximumObservations,
                captureDeadlineUtc = capture.CaptureRequestedUtc + capture.CaptureTimeout,
                sha256 = evidence?.Sha256,
                basicQualityPassed = evidence?.PassesBasicQuality,
                qualityVerdict = evidence?.QualityVerdict.ToString(),
                qualityReason = evidence?.QualityReason,
                qualityScope = "basic_pixel_sanity_only",
                semanticVisualCorrectnessEvaluated = false,
            });
        }

        private LiveTestResponse HandleRenderStatus(LiveTestRequest request)
        {
            if (isServer)
            {
                return Failure(
                    request.Id,
                    "method_not_allowed",
                    "Render status is only available in a client process.",
                    false);
            }

            return ExecuteOnGameThread(request, () =>
            {
                long privateBytes;
                using (Process process = Process.GetCurrentProcess())
                {
                    privateBytes = process.PrivateMemorySize64;
                }

                return Success(request.Id, new
                {
                    experimental = true,
                    nativeRenderStateConfirmed = false,
                    engineFrame = Utilities.EngineFrameNo,
                    mainFps = Utilities.GetMainFps(),
                    rendererFps = Utilities.GetRendererFps(),
                    processPrivateBytes = privateBytes,
                    gameThreadQueueDepth = GameThread.Instance.QueueLength,
                    activeState = GameStateManager.Current?.ActiveState?.GetType().FullName,
                    topScreen = ScreenManager.TopScreen?.GetType().FullName,
                    activeMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
                    missionScene = Mission.Current?.SceneName,
                    campaignLoaded = Campaign.Current != null,
                    missionActive = Mission.Current != null,
                    trackedRenderingEnabled,
                    renderStateTrackable,
                    renderToggleCount,
                    trackingBasis = "assumed_enabled_at_live_test_start_then_requested_transitions",
                });
            }, false);
        }

        private LiveTestResponse HandleRenderToggle(LiveTestRequest request)
        {
            if (isServer)
            {
                return Failure(
                    request.Id,
                    "method_not_allowed",
                    "Render toggling is only available in a client process.",
                    false);
            }

            if (!TryReadBoolean(request.Parameters, "enabled", out var requestedEnabled))
            {
                return Failure(
                    request.Id,
                    "invalid_parameters",
                    "Render toggle parameters require a boolean 'enabled'.",
                    false);
            }

            return ExecuteOnGameThread(request, () =>
            {
                if (!renderStateTrackable)
                {
                    return Failure(
                        request.Id,
                        "render_state_untrackable",
                        "A prior experimental native render transition failed, so another toggle would be unsafe.",
                        true);
                }

                bool toggleInvoked = requestedEnabled != trackedRenderingEnabled;
                if (toggleInvoked)
                {
                    try
                    {
                        // Native semantics are unknown; live canaries must prove frame, network, and screenshot behavior.
                        Utilities.ToggleRender();
                        trackedRenderingEnabled = requestedEnabled;
                        renderToggleCount++;
                    }
                    catch
                    {
                        renderStateTrackable = false;
                        throw;
                    }
                }

                return Success(request.Id, new
                {
                    experimental = true,
                    requestedEnabled,
                    trackedRenderingEnabled,
                    renderStateTrackable,
                    toggleInvoked,
                    renderToggleCount,
                    nativeRenderStateConfirmed = false,
                    trackingBasis = "assumed_enabled_at_live_test_start_then_requested_transitions",
                });
            }, true);
        }

        private LiveTestResponse HandleShutdown(LiveTestRequest request)
        {
            bool newlyScheduled = Interlocked.Exchange(ref shutdownScheduled, 1) == 0;
            if (newlyScheduled)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(250);
                    GameThread.RunSafe(
                        Utilities.QuitGame,
                        context: "LiveTestControl.shutdown");
                });
            }

            return Success(request.Id, new
            {
                scheduled = newlyScheduled,
            });
        }

        private LiveTestResponse HandleDeferredClientJoin(LiveTestRequest request)
        {
            if (isServer)
            {
                return Failure(
                    request.Id,
                    "method_not_allowed",
                    "Only a client process may join a co-op session.",
                    false);
            }

            if (!deferredClientJoinEnabled)
            {
                return Failure(
                    request.Id,
                    "manual_join_not_enabled",
                    "The client was not launched for a deferred live-test join.",
                    false);
            }

            return ExecuteOnGameThread(request, () =>
            {
                if (Volatile.Read(ref deferredClientJoinAttempted) != 0)
                {
                    return Failure(
                        request.Id,
                        "join_already_attempted",
                        "The deferred client join was already attempted.",
                        false);
                }

                if (!(GameStateManager.Current?.ActiveState is InitialState) || Campaign.Current != null)
                {
                    return Failure(
                        request.Id,
                        "client_not_at_main_menu",
                        "The client must be at the main menu with no campaign loaded before joining.",
                        false);
                }

                if (Interlocked.Exchange(ref deferredClientJoinAttempted, 1) != 0)
                {
                    return Failure(
                        request.Id,
                        "join_already_attempted",
                        "The deferred client join was already attempted.",
                        false);
                }

                bool started = startAsClient();
                Logger.Information("[LiveTest] Deferred StartAsClient() returned {Started}", started);
                return Success(request.Id, new
                {
                    attempted = true,
                    started,
                });
            }, true);
        }

        private LiveTestResponse CreateStatusResponse(string requestId)
        {
            bool campaignLoaded = Campaign.Current != null;
            bool missionActive = Mission.Current != null;
            bool coopRunning = false;
            string coopState = null;
            int? registeredPlayers = null;
            int? registeredPlayerCount = null;
            int? connectedPlayerCount = null;
            string[] registeredControllerIds = null;
            string[] connectedControllerIds = null;

            if (campaignLoaded && ContainerProvider.TryResolve<ILogic>(out var logic))
            {
                try
                {
                    coopRunning = logic.RunningState;
                    if (logic is IClientLogic clientLogic)
                    {
                        coopState = clientLogic.State?.GetType().FullName;
                    }
                    else if (logic is IServerLogic serverLogic)
                    {
                        coopState = serverLogic.State?.GetType().FullName;
                    }
                }
                catch (Exception exception)
                {
                    Logger.Debug(exception, "[LiveTest] Co-op state is not readable yet");
                }

                if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                {
                    var players = playerManager.Players
                        .OrderBy(player => player.ControllerId, StringComparer.Ordinal)
                        .ToArray();
                    registeredPlayers = players.Length;
                    if (logic is IServerLogic)
                    {
                        registeredPlayerCount = players.Length;
                        registeredControllerIds = players
                            .Select(player => player.ControllerId)
                            .ToArray();
                        connectedControllerIds = players
                            .Where(playerManager.IsConnected)
                            .Select(player => player.ControllerId)
                            .ToArray();
                        connectedPlayerCount = connectedControllerIds.Length;
                    }
                }
            }

            bool commandRegistryReady =
                ContainerProvider.TryResolve<ILiveTestCommandDispatcher>(out var dispatcher) &&
                dispatcher.EnsureReady();
            string activeState = GameStateManager.Current?.ActiveState?.GetType().FullName;
            string topScreen = ScreenManager.TopScreen?.GetType().FullName;
            string activeMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
            bool readyForCampaignTests = campaignLoaded && coopRunning && commandRegistryReady;
            string[] modAssemblyNames =
            {
                "Common",
                "GameInterface",
                "Coop.Core",
                "Missions",
                "Coop",
                "Coop.Steam",
            };
            bool coopSteamLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                string.Equals(assembly.GetName().Name, "Coop.Steam", StringComparison.Ordinal));
            if (!coopSteamLoaded)
            {
                string coopAssemblyDirectory = System.IO.Path.GetDirectoryName(typeof(CoopMod).Assembly.Location);
                if (!string.IsNullOrEmpty(coopAssemblyDirectory))
                {
                    string coopSteamAssemblyPath = System.IO.Path.Combine(coopAssemblyDirectory, "Coop.Steam.dll");
                    if (File.Exists(coopSteamAssemblyPath))
                    {
                        try
                        {
                            System.Reflection.Assembly.LoadFrom(coopSteamAssemblyPath);
                        }
                        catch (Exception exception)
                        {
                            Logger.Debug(exception, "[LiveTest] Could not load {AssemblyPath} for the status probe", coopSteamAssemblyPath);
                        }
                    }
                }
            }
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => modAssemblyNames.Contains(
                    assembly.GetName().Name,
                    StringComparer.Ordinal))
                .OrderBy(assembly => assembly.GetName().Name)
                .Select(assembly => new
                {
                    name = assembly.GetName().Name,
                    version = assembly.GetName().Version?.ToString(),
                    mvid = assembly.ManifestModule.ModuleVersionId,
                    location = assembly.Location,
                })
                .ToArray();

            return Success(requestId, new
            {
                protocolVersion = LiveTestProtocol.Version,
                pid = processInfo.Pid,
                role = processInfo.Role,
                platformId = processInfo.PlatformId,
                runToken = processInfo.RunToken,
                buildVersion = ModInformation.BuildVersion,
                assemblyMvid = typeof(CoopMod).Assembly.ManifestModule.ModuleVersionId,
                loadedAssemblies,
                processStartedUtc,
                logPath = logFilePath,
                gameThreadInitialized = GameThread.Instance.IsInitialized,
                gameThreadQueueDepth = GameThread.Instance.QueueLength,
                commandRegistryReady,
                activeState,
                topScreen,
                activeMenu,
                campaignLoaded,
                missionActive,
                coopRunning,
                coopState,
                registeredPlayers,
                registeredPlayerCount,
                connectedPlayerCount,
                registeredControllerIds,
                connectedControllerIds,
                deferredClientJoinEnabled,
                readyForClientJoin = !isServer && deferredClientJoinEnabled &&
                    Volatile.Read(ref deferredClientJoinAttempted) == 0 &&
                    GameStateManager.Current?.ActiveState is InitialState && !campaignLoaded,
                deferredClientJoinAttempted = Volatile.Read(ref deferredClientJoinAttempted) != 0,
                readyForCampaignTests,
                readyForMissionTests = readyForCampaignTests && missionActive,
            });
        }

        private LiveTestResponse ExecuteOnGameThread(
            LiveTestRequest request,
            Func<LiveTestResponse> operation,
            bool timeoutOutcomeUncertain)
        {
            LiveTestResponse response = null;
            Exception operationException = null;

            try
            {
                GameThread.Run(() =>
                {
                    try
                    {
                        response = operation();
                    }
                    catch (Exception exception)
                    {
                        operationException = exception;
                    }
                }, blocking: true, label: "LiveTestControl." + request.Method);
            }
            catch (TimeoutException exception)
            {
                Logger.Error(exception, "[LiveTest] Game-thread timeout for {Method} request {RequestId}", request.Method, request.Id);
                return Failure(
                    request.Id,
                    "game_thread_timeout",
                    "The game thread did not complete the request within 30 seconds. The operation may still run later.",
                    timeoutOutcomeUncertain);
            }

            if (operationException != null)
            {
                Logger.Error(operationException, "[LiveTest] {Method} request {RequestId} failed", request.Method, request.Id);
                return Failure(
                    request.Id,
                    "operation_failed",
                    operationException.Message,
                    timeoutOutcomeUncertain);
            }

            return response ?? Failure(
                request.Id,
                "empty_response",
                "The game-thread operation returned no response.",
                timeoutOutcomeUncertain);
        }

        private bool TryReadEngineObservation(
            LiveTestRequest request,
            out DateTime observationUtc,
            out int observationEngineFrame,
            out LiveTestResponse failure)
        {
            DateTime observedUtc = default(DateTime);
            int observedFrame = 0;
            try
            {
                GameThread.Run(() =>
                {
                    observedUtc = DateTime.UtcNow;
                    observedFrame = Utilities.EngineFrameNo;
                }, blocking: true, label: "LiveTestControl." + request.Method + ".frame");
            }
            catch (TimeoutException exception)
            {
                Logger.Error(exception, "[LiveTest] Game-thread timeout for {Method} request {RequestId}", request.Method, request.Id);
                observationUtc = default(DateTime);
                observationEngineFrame = 0;
                failure = Failure(
                    request.Id,
                    "game_thread_timeout",
                    "The game thread did not provide a screenshot observation frame within 30 seconds.",
                    false);
                return false;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "[LiveTest] Could not read screenshot observation frame for request {RequestId}", request.Id);
                observationUtc = default(DateTime);
                observationEngineFrame = 0;
                failure = Failure(
                    request.Id,
                    "operation_failed",
                    exception.Message,
                    false);
                return false;
            }

            observationUtc = observedUtc;
            observationEngineFrame = observedFrame;
            failure = null;
            return true;
        }

        private LiveTestResponse Success(string id, object result)
        {
            return LiveTestResponse.Success(id, GetProcessInfo(), result);
        }

        private LiveTestResponse Failure(
            string id,
            string code,
            string message,
            bool outcomeUncertain)
        {
            return LiveTestResponse.Failure(
                id,
                GetProcessInfo(),
                new LiveTestError(code, message, outcomeUncertain));
        }

        private LiveTestProcessInfo GetProcessInfo()
        {
            return new LiveTestProcessInfo
            {
                Pid = processInfo.Pid,
                ProcessStartedUtc = processStartedUtc,
                Role = processInfo.Role,
                PlatformId = processInfo.PlatformId,
                RunToken = processInfo.RunToken,
            };
        }

        private void WriteEndpointRegistration()
        {
            Directory.CreateDirectory(endpointDirectory);
            string temporaryPath = endpointRegistrationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string registration = JsonSerializer.Serialize(new
            {
                version = LiveTestProtocol.Version,
                pid = processInfo.Pid,
                role = processInfo.Role,
                platformId = processInfo.PlatformId,
                runToken = processInfo.RunToken,
                processStartedUtc,
                pipeName = LiveTestProtocol.GetPipeName(processInfo.Pid),
            });

            try
            {
                File.WriteAllText(temporaryPath, registration);
                if (File.Exists(endpointRegistrationPath))
                {
                    File.Replace(temporaryPath, endpointRegistrationPath, null);
                }
                else
                {
                    File.Move(temporaryPath, endpointRegistrationPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private void DeleteEndpointRegistration()
        {
            try
            {
                if (File.Exists(endpointRegistrationPath))
                {
                    File.Delete(endpointRegistrationPath);
                }

                if (Directory.Exists(endpointDirectory) &&
                    Directory.GetFileSystemEntries(endpointDirectory).Length == 0)
                {
                    Directory.Delete(endpointDirectory);
                }
            }
            catch (Exception exception)
            {
                Logger.Warning(
                    exception,
                    "[LiveTest] Failed to remove endpoint registration {RegistrationPath}",
                    endpointRegistrationPath);
            }
        }

        private static bool TryParseStructuredResult(string output, out object structuredResult)
        {
            const string prefix = "LIVE_TEST_JSON=";
            structuredResult = null;
            string json = null;
            int matches = 0;

            using (var reader = new StringReader(output ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;

                    matches++;
                    json = line.Substring(prefix.Length);
                }
            }

            if (matches != 1 || string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    structuredResult = document.RootElement.Clone();
                }
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool IsLowerHexadecimal(char character) =>
            (character >= '0' && character <= '9') ||
            (character >= 'a' && character <= 'f');

        private static bool TryReadCommand(
            JsonElement parameters,
            out string command,
            out List<string> arguments,
            out string error)
        {
            command = null;
            arguments = null;
            error = null;

            if (!TryReadString(parameters, "name", out command) || string.IsNullOrWhiteSpace(command))
            {
                error = "Command parameters require a non-empty string 'name'.";
                return false;
            }

            if (!parameters.TryGetProperty("arguments", out var argumentElement) ||
                argumentElement.ValueKind != JsonValueKind.Array)
            {
                error = "Command parameters require an 'arguments' array.";
                return false;
            }

            arguments = new List<string>();
            foreach (JsonElement argument in argumentElement.EnumerateArray())
            {
                if (argument.ValueKind != JsonValueKind.String)
                {
                    error = "Every command argument must be a string.";
                    return false;
                }

                arguments.Add(argument.GetString());
            }

            return true;
        }

        private static bool TryReadString(JsonElement parameters, string propertyName, out string value)
        {
            value = null;
            return parameters.TryGetProperty(propertyName, out var element) &&
                element.ValueKind == JsonValueKind.String &&
                (value = element.GetString()) != null;
        }

        private static bool TryReadBoolean(JsonElement parameters, string propertyName, out bool value)
        {
            value = false;
            if (!parameters.TryGetProperty(propertyName, out var element) ||
                (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False))
            {
                return false;
            }

            value = element.GetBoolean();
            return true;
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            int index = Array.FindIndex(arguments, argument =>
                argument.Equals(name, StringComparison.OrdinalIgnoreCase));

            return index >= 0 && index + 1 < arguments.Length
                ? arguments[index + 1]
                : null;
        }

        private static string NormalizeRunToken(string runToken)
        {
            if (string.IsNullOrWhiteSpace(runToken) || runToken.Length > 64) return null;

            return runToken.All(character =>
                char.IsLetterOrDigit(character) || character == '-' || character == '_')
                ? runToken
                : null;
        }

    }
}
#endif
