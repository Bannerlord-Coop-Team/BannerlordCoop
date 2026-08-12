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
        private const int MaxTrackedScreenshotCaptures = 64;
        private static readonly TimeSpan ScreenshotCaptureTimeout = TimeSpan.FromMinutes(5);
        private static readonly ILogger Logger = LogManager.GetLogger<LiveTestControlServer>();

        private readonly string logFilePath;
        private readonly LiveTestProcessInfo processInfo;
        private readonly DateTime processStartedUtc;
        private readonly NamedPipeLiveTestServer pipeServer;
        private readonly object screenshotCapturesLock = new object();
        private readonly Dictionary<string, ScreenshotCaptureState> screenshotCaptures =
            new Dictionary<string, ScreenshotCaptureState>(StringComparer.Ordinal);
        private readonly string endpointDirectory;
        private readonly string endpointRegistrationPath;
        private int shutdownScheduled;

        public LiveTestControlServer(bool isServer, string logFilePath)
        {
            if (string.IsNullOrWhiteSpace(logFilePath)) throw new ArgumentException("A log file path is required.", nameof(logFilePath));

            this.logFilePath = logFilePath;

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
                    return Failure(
                        request.Id,
                        "screenshot_path_exists",
                        "Screenshot path must not already exist.",
                        false);
                }

                string captureId = Guid.NewGuid().ToString("N");
                lock (screenshotCapturesLock)
                {
                    if (screenshotCaptures.Count >= MaxTrackedScreenshotCaptures)
                    {
                        DateTime now = DateTime.UtcNow;
                        string removableCaptureId = screenshotCaptures
                            .Where(pair => pair.Value.CanDiscard(now))
                            .OrderBy(pair => pair.Value.RequestedAtUtc)
                            .Select(pair => pair.Key)
                            .FirstOrDefault();
                        if (removableCaptureId == null)
                        {
                            return Failure(
                                request.Id,
                                "screenshot_capacity_reached",
                                "Too many screenshot captures are still pending.",
                                false);
                        }
                        screenshotCaptures.Remove(removableCaptureId);
                    }
                    screenshotCaptures.Add(captureId, new ScreenshotCaptureState(screenshotPath));
                }

                try
                {
                    Utilities.TakeScreenshot(screenshotPath);
                }
                catch
                {
                    lock (screenshotCapturesLock)
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
                !Guid.TryParseExact(captureId, "N", out _))
            {
                return Failure(
                    request.Id,
                    "invalid_parameters",
                    "Screenshot capture id must be a 32-character hexadecimal value.",
                    false);
            }

            ScreenshotCaptureState capture;
            lock (screenshotCapturesLock)
            {
                if (!screenshotCaptures.TryGetValue(captureId, out capture))
                {
                    return Failure(
                        request.Id,
                        "capture_not_found",
                        $"Screenshot capture '{captureId}' is not registered.",
                        false);
                }

                ScreenshotCaptureObservation observation = capture.Observe();
                return Success(request.Id, new
                {
                    captureId,
                    path = capture.Path,
                    complete = observation.Complete,
                    exists = observation.Exists,
                    isBmp = observation.IsBmp,
                    stable = observation.Stable,
                    length = observation.Length,
                    declaredLength = observation.DeclaredLength,
                    lastWriteUtc = observation.LastWriteUtc,
                });
            }
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
                        using (AllowedThread.Suspend())
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

        private sealed class ScreenshotCaptureState
        {
            private DateTime? completedAtUtc;
            private long? observedLength;
            private DateTime? observedLastWriteUtc;

            public ScreenshotCaptureState(string path)
            {
                Path = path;
                RequestedAtUtc = DateTime.UtcNow;
            }

            public string Path { get; }

            public DateTime RequestedAtUtc { get; }

            public bool CanDiscard(DateTime now)
            {
                return completedAtUtc.HasValue || now - RequestedAtUtc >= ScreenshotCaptureTimeout;
            }

            public ScreenshotCaptureObservation Observe()
            {
                bool exists = File.Exists(Path);
                long length = 0;
                DateTime? lastWriteUtc = null;
                bool isBmp = false;
                uint declaredLength = 0;
                if (exists)
                {
                    try
                    {
                        var file = new FileInfo(Path);
                        length = file.Length;
                        lastWriteUtc = file.LastWriteTimeUtc;
                        isBmp = TryReadBitmapDeclaredLength(Path, out declaredLength);
                    }
                    catch (IOException)
                    {
                        exists = false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        exists = false;
                    }
                }

                bool stable = exists &&
                    isBmp &&
                    declaredLength == length &&
                    length > 0 &&
                    observedLength.HasValue &&
                    observedLength.Value == length &&
                    observedLastWriteUtc.HasValue &&
                    lastWriteUtc.HasValue &&
                    observedLastWriteUtc.Value == lastWriteUtc.Value;
                bool validObservation = exists &&
                    isBmp &&
                    declaredLength == length &&
                    length > 0 &&
                    lastWriteUtc.HasValue;
                observedLength = validObservation ? length : (long?)null;
                observedLastWriteUtc = validObservation ? lastWriteUtc : null;
                if (stable)
                {
                    completedAtUtc = DateTime.UtcNow;
                }

                return new ScreenshotCaptureObservation(
                    exists,
                    isBmp,
                    stable,
                    length,
                    declaredLength,
                    lastWriteUtc?.ToString("o"));
            }

            private static bool TryReadBitmapDeclaredLength(string path, out uint declaredLength)
            {
                declaredLength = 0;
                try
                {
                    using (var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    {
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
                        if (isBmp)
                            declaredLength = BitConverter.ToUInt32(header, 2);
                        return isBmp;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
        }

        private sealed class ScreenshotCaptureObservation
        {
            public ScreenshotCaptureObservation(
                bool exists,
                bool isBmp,
                bool stable,
                long length,
                uint declaredLength,
                string lastWriteUtc)
            {
                Exists = exists;
                IsBmp = isBmp;
                Stable = stable;
                Length = length;
                DeclaredLength = declaredLength;
                LastWriteUtc = lastWriteUtc;
            }

            public bool Complete => Exists && IsBmp && Stable;

            public bool Exists { get; }

            public bool IsBmp { get; }

            public bool Stable { get; }

            public long Length { get; }

            public uint DeclaredLength { get; }

            public string LastWriteUtc { get; }
        }

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

    }
}
#endif
