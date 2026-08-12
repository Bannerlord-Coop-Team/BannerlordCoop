#if DEBUG
using Common;
using Common.LiveTesting;
using Common.Logging;
using Common.LogicStates;
using Common.Util;
using Coop.Core.Client;
using Coop.Core.Server;
using GameInterface;
using GameInterface.Services.LiveTesting;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly ILogger Logger = LogManager.GetLogger<LiveTestControlServer>();

        private readonly string logFilePath;
        private readonly bool isServer;
        private readonly bool deferredClientJoinEnabled;
        private readonly Func<bool> startAsClient;
        private readonly LiveTestProcessInfo processInfo;
        private readonly DateTime processStartedUtc;
        private readonly NamedPipeLiveTestServer pipeServer;
        private readonly object screenshotGate = new object();
        private readonly Dictionary<string, ScreenshotCapture> screenshotCaptures =
            new Dictionary<string, ScreenshotCapture>(StringComparer.Ordinal);
        private int shutdownScheduled;
        private int deferredClientJoinAttempted;

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
                Role = isServer ? "server" : "client",
                PlatformId = ReadArgument(arguments, "/platformId"),
                RunToken = NormalizeRunToken(ReadArgument(arguments, "/cooptestrun")),
            };

            string pipeName = LiveTestProtocol.GetPipeName(processId);
            pipeServer = new NamedPipeLiveTestServer(pipeName, GetProcessInfo, Handle);
        }

        public void Start()
        {
            pipeServer.Start();
            Logger.Information(
                "[LiveTest] Listening on {PipeName} as {Role} (platform {PlatformId}, run {RunToken})",
                LiveTestProtocol.GetPipeName(processInfo.Pid),
                processInfo.Role,
                processInfo.PlatformId ?? "none",
                processInfo.RunToken ?? "unscoped");
        }

        public void Dispose()
        {
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

        private LiveTestResponse HandleCommand(LiveTestRequest request)
        {
            if (!TryReadCommand(request.Parameters, out var command, out var arguments, out var error))
            {
                return Failure(request.Id, "invalid_parameters", error, false);
            }

            if (!command.StartsWith("coop.debug.", StringComparison.Ordinal))
            {
                return Failure(
                    request.Id,
                    "command_not_allowed",
                    "Only coop.debug.* commands may be run through live testing.",
                    false);
            }

            return ExecuteOnGameThread(request, () =>
            {
                if (!ContainerProvider.TryResolve<ILiveTestCommandDispatcher>(out var dispatcher))
                {
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

                return Success(request.Id, new
                {
                    name = command,
                    arguments,
                    found = true,
                    output = result.Output,
                });
            }, true);
        }

        private LiveTestResponse HandleCommandCatalog(LiveTestRequest request)
        {
            return ExecuteOnGameThread(request, () =>
            {
                if (!ContainerProvider.TryResolve<ILiveTestCommandDispatcher>(out var dispatcher))
                {
                    return Failure(
                        request.Id,
                        "session_not_ready",
                        "The co-op session command dispatcher is not available yet.",
                        false);
                }

                return Success(request.Id, new
                {
                    commands = dispatcher.GetCommandNames(),
                });
            }, false);
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

                Directory.CreateDirectory(directory);
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                }

                lock (screenshotGate)
                {
                    foreach (string previousCaptureId in screenshotCaptures
                        .Where(pair => string.Equals(
                            pair.Value.Path,
                            screenshotPath,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(pair => pair.Key)
                        .ToArray())
                    {
                        screenshotCaptures.Remove(previousCaptureId);
                    }

                    screenshotCaptures.Add(
                        captureId,
                        new ScreenshotCapture(screenshotPath));
                }

                try
                {
                    Utilities.TakeScreenshot(screenshotPath);
                }
                catch
                {
                    lock (screenshotGate)
                    {
                        screenshotCaptures.Remove(captureId);
                    }
                    throw;
                }

                return Success(request.Id, new
                {
                    captureId,
                    path = screenshotPath,
                    captureRequested = true,
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

            ScreenshotCapture capture;
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

            ScreenshotFileObservation observation = ObserveScreenshot(capture.Path);
            bool stable;
            bool complete;
            lock (screenshotGate)
            {
                if (!screenshotCaptures.TryGetValue(captureId, out var currentCapture) ||
                    !ReferenceEquals(capture, currentCapture))
                {
                    return Failure(
                        request.Id,
                        "capture_not_found",
                        $"Screenshot capture '{captureId}' was superseded.",
                        false);
                }

                stable = observation.Exists &&
                    observation.IsBmp &&
                    observation.DeclaredLength == observation.Length &&
                    observation.Length > 0 &&
                    capture.HasObservation &&
                    capture.LastLength == observation.Length &&
                    capture.LastWriteUtc == observation.LastWriteUtc;
                capture.HasObservation = observation.Exists;
                capture.LastLength = observation.Length;
                capture.LastWriteUtc = observation.LastWriteUtc;
                capture.Complete |= stable;
                complete = capture.Complete;
            }

            return Success(request.Id, new
            {
                captureId,
                path = capture.Path,
                exists = observation.Exists,
                isBmp = observation.IsBmp,
                stable,
                complete,
                length = observation.Length,
                declaredLength = observation.DeclaredLength,
                lastWriteUtc = observation.LastWriteUtc,
            });
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
            }, true, false);
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
                    if (ModInformation.IsServer)
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
                deferredClientJoinAttempted = Volatile.Read(ref deferredClientJoinAttempted) != 0,
                readyForCampaignTests,
                readyForMissionTests = readyForCampaignTests && missionActive,
            });
        }

        private LiveTestResponse ExecuteOnGameThread(
            LiveTestRequest request,
            Func<LiveTestResponse> operation,
            bool timeoutOutcomeUncertain,
            bool suspendPatches = true)
        {
            LiveTestResponse response = null;
            Exception operationException = null;

            try
            {
                GameThread.Run(() =>
                {
                    try
                    {
                        if (suspendPatches)
                        {
                            using (AllowedThread.Suspend())
                            {
                                response = operation();
                            }
                        }
                        else
                        {
                            response = operation();
                        }
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
                Role = processInfo.Role,
                PlatformId = processInfo.PlatformId,
                RunToken = processInfo.RunToken,
            };
        }

        private static ScreenshotFileObservation ObserveScreenshot(string path)
        {
            if (!File.Exists(path)) return ScreenshotFileObservation.Missing;

            try
            {
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    long length = stream.Length;
                    var header = new byte[6];
                    int headerLength = 0;
                    while (headerLength < header.Length)
                    {
                        int bytesRead = stream.Read(
                            header,
                            headerLength,
                            header.Length - headerLength);
                        if (bytesRead == 0) break;
                        headerLength += bytesRead;
                    }
                    bool isBmp = headerLength == header.Length &&
                        header[0] == 'B' &&
                        header[1] == 'M';
                    uint declaredLength = isBmp
                        ? BitConverter.ToUInt32(header, 2)
                        : 0;
                    return new ScreenshotFileObservation(
                        true,
                        isBmp,
                        length,
                        declaredLength,
                        File.GetLastWriteTimeUtc(path));
                }
            }
            catch (IOException)
            {
                return new ScreenshotFileObservation(true, false, 0, 0, null);
            }
            catch (UnauthorizedAccessException)
            {
                return new ScreenshotFileObservation(true, false, 0, 0, null);
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

        private sealed class ScreenshotCapture
        {
            public string Path { get; }
            public bool HasObservation { get; set; }
            public long LastLength { get; set; }
            public DateTime? LastWriteUtc { get; set; }
            public bool Complete { get; set; }

            public ScreenshotCapture(string path)
            {
                Path = path;
            }
        }

        private readonly struct ScreenshotFileObservation
        {
            public static readonly ScreenshotFileObservation Missing =
                new ScreenshotFileObservation(false, false, 0, 0, null);

            public bool Exists { get; }
            public bool IsBmp { get; }
            public long Length { get; }
            public uint DeclaredLength { get; }
            public DateTime? LastWriteUtc { get; }

            public ScreenshotFileObservation(
                bool exists,
                bool isBmp,
                long length,
                uint declaredLength,
                DateTime? lastWriteUtc)
            {
                Exists = exists;
                IsBmp = isBmp;
                Length = length;
                DeclaredLength = declaredLength;
                LastWriteUtc = lastWriteUtc;
            }
        }
    }
}
#endif
