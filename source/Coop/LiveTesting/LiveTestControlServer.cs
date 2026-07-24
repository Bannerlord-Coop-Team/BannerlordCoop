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
        private readonly LiveTestProcessInfo processInfo;
        private readonly DateTime processStartedUtc;
        private readonly NamedPipeLiveTestServer pipeServer;
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
                case "command":
                    return HandleCommand(request);
                case "screenshot":
                    return HandleScreenshot(request);
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
                Utilities.TakeScreenshot(screenshotPath);
                return Success(request.Id, new
                {
                    path = screenshotPath,
                    captureRequested = true,
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

        private LiveTestResponse CreateStatusResponse(string requestId)
        {
            bool campaignLoaded = Campaign.Current != null;
            bool missionActive = Mission.Current != null;
            bool coopRunning = false;
            string coopState = null;
            int? registeredPlayers = null;

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
                    registeredPlayers = playerManager.Players.Count;
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
