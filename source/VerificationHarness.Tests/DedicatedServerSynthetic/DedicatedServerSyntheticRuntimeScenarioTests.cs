using Common.LiveTesting;
using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticRuntimeScenarioTests
{
    [Fact]
    public async Task RunAsync_ExecutesTheCompleteLifecycleWithFiveClientResults()
    {
        string passwordVariable = SetPassword();
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(passwordVariable);
            var clientFactory = new StubClientFactory();
            var scenario = new DedicatedServerSyntheticRuntimeScenario(
                new LifecycleControlClient(),
                new DedicatedServerControlResponseValidator(),
                clientFactory);

            DedicatedServerSyntheticScenarioResult result = await scenario.RunAsync(
                options,
                CancellationToken.None);

            Assert.True(result.Completed);
            Assert.True(result.WrongPasswordRejected);
            Assert.True(result.IncompatibleModuleRejected);
            Assert.True(result.CompatibleModuleHandshakeCompleted);
            Assert.True(result.ProtocolShortcut);
            Assert.Equal(5, result.Clients.Count);
            Assert.Equal(5, clientFactory.CreateCount);
            Assert.Equal(
                new[]
                {
                    "before-connect",
                    "two-connected",
                    "one-disconnected",
                    "reconnected",
                    "final-empty"
                },
                result.Lifecycle.Select(snapshot => snapshot.Phase));
            Assert.All(result.Lifecycle, snapshot =>
            {
                Assert.True(snapshot.ControlEnvelopeValidated);
                Assert.True(snapshot.ControlRequestIdentityValidated);
                Assert.True(snapshot.DedicatedProcessIdentityValidated);
                Assert.True(snapshot.FirstClassConnectionRosterValidated);
            });
            Assert.Empty(result.FailureCodes);
        }
        finally
        {
            Environment.SetEnvironmentVariable(passwordVariable, null);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsAReusedReconnectIdentity()
    {
        string passwordVariable = SetPassword();
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(passwordVariable);
            var scenario = new DedicatedServerSyntheticRuntimeScenario(
                new LifecycleControlClient(reuseConnectionIdentity: true),
                new DedicatedServerControlResponseValidator(),
                new StubClientFactory());

            DedicatedServerSyntheticScenarioResult result = await scenario.RunAsync(
                options,
                CancellationToken.None);

            Assert.False(result.Completed);
            Assert.False(result.ProtocolShortcut);
            Assert.Contains("reconnect-reused-connection-instance", result.FailureCodes);
            Assert.Equal("final-empty", result.Lifecycle.Last().Phase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(passwordVariable, null);
        }
    }

    private static DedicatedServerSyntheticOptions CreateOptions(string passwordVariable)
    {
        return DedicatedServerSyntheticOptions.Parse(new[]
        {
            "--head", new string('a', 40),
            "--tree", new string('b', 40),
            "--server-head", new string('c', 40),
            "--server-tree", new string('d', 40),
            "--server-pid", "1234",
            "--run-token", "run-token",
            "--request-id", "request-id",
            "--join-port", "4201",
            "--timeout-ms", "2000",
            "--seed", "17",
            "--artifact-manifest", "staged-artifacts.json",
            "--artifact-manifest-sha256", new string('7', 64),
            "--artifact-root", "staged-runtime",
            "--password-env", passwordVariable
        });
    }

    private static string SetPassword()
    {
        string variable = "DS_SYNTHETIC_RUNTIME_SCENARIO_PASSWORD_" +
                          Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "runtime-scenario-password");
        return variable;
    }

    private sealed class LifecycleControlClient : IDedicatedServerControlClient
    {
        private readonly bool reuseConnectionIdentity;
        private int requestIndex;

        public LifecycleControlClient(bool reuseConnectionIdentity = false)
        {
            this.reuseConnectionIdentity = reuseConnectionIdentity;
        }

        public Task<string> GetStatusAsync(
            int processId,
            string requestId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            DedicatedServerRosterEntry Client(string controllerId, string connectionId) =>
                new(controllerId, connectionId, true, "ResolveCharacterState");

            DedicatedServerRosterEntry[] roster = requestIndex++ switch
            {
                0 => Array.Empty<DedicatedServerRosterEntry>(),
                1 => new[]
                {
                    Client("ds-synthetic-client-a", "connection-a-1"),
                    Client("ds-synthetic-client-b", "connection-b-1")
                },
                2 => new[]
                {
                    Client("ds-synthetic-client-b", "connection-b-1")
                },
                3 => new[]
                {
                    Client(
                        "ds-synthetic-client-a",
                        reuseConnectionIdentity ? "connection-a-1" : "connection-a-2"),
                    Client("ds-synthetic-client-b", "connection-b-1")
                },
                4 => Array.Empty<DedicatedServerRosterEntry>(),
                _ => throw new InvalidOperationException("The scenario requested an extra snapshot.")
            };

            string response = LiveTestProtocol.SerializeResponse(new LiveTestResponse
            {
                Id = requestId,
                Ok = true,
                Process = new LiveTestProcessInfo
                {
                    Pid = processId,
                    Role = "server",
                    RunToken = "run-token"
                },
                Result = new
                {
                    serving = true,
                    joinPort = 4201,
                    moduleValidation = ModuleValidation(),
                    connectionRoster = roster.Select(entry => new
                    {
                        controllerId = entry.ControllerId,
                        connectionInstanceId = entry.ConnectionInstanceId,
                        connected = entry.Connected,
                        joinState = entry.JoinState
                    }).ToArray()
                }
            });
            return Task.FromResult(response);
        }

        private static object ModuleValidation()
        {
            return new
            {
                coopBuildVersion = "coop-build",
                modules = new[]
                {
                    new
                    {
                        id = "Native",
                        isOfficial = true,
                        isDlc = false,
                        version = new
                        {
                            applicationVersionType = 4,
                            major = 1,
                            minor = 2,
                            revision = 3,
                            changeSet = 456
                        }
                    }
                }
            };
        }
    }

    private sealed class StubClientFactory : IDedicatedServerSyntheticClientFactory
    {
        public int CreateCount { get; private set; }

        public IDedicatedServerSyntheticClientSession Create(
            DedicatedServerSyntheticNodeOptions options,
            bool holdConnection)
        {
            CreateCount++;
            DedicatedServerSyntheticNodeResult result = options.Scenario switch
            {
                DedicatedServerSyntheticNodeOptions.WrongPasswordScenario => new()
                {
                    Scenario = DedicatedServerSyntheticNodeOptions.WrongPasswordScenario,
                    Success = true,
                    RejectedPasswords = 1
                },
                DedicatedServerSyntheticNodeOptions.ModuleMismatchScenario => new()
                {
                    Scenario = DedicatedServerSyntheticNodeOptions.ModuleMismatchScenario,
                    Success = true,
                    ModuleDenialsObserved = 1
                },
                _ => new DedicatedServerSyntheticNodeResult
                {
                    Scenario = DedicatedServerSyntheticNodeOptions.BaselineScenario,
                    Success = true,
                    ModuleMatchesObserved = 1,
                    FreshControllerResultsObserved = 1,
                    ProtocolShortcut = true
                }
            };
            return new StubClientSession(result, holdConnection);
        }
    }

    private sealed class StubClientSession : IDedicatedServerSyntheticClientSession
    {
        private readonly DedicatedServerSyntheticNodeResult result;
        private readonly bool holdConnection;
        private readonly TaskCompletionSource<DedicatedServerSyntheticNodeResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StubClientSession(
            DedicatedServerSyntheticNodeResult result,
            bool holdConnection)
        {
            this.result = result;
            this.holdConnection = holdConnection;
        }

        public Task<bool> Ready => Task.FromResult(true);

        public Task<DedicatedServerSyntheticNodeResult> RunAsync(
            CancellationToken cancellationToken)
        {
            return holdConnection
                ? completion.Task.WaitAsync(cancellationToken)
                : Task.FromResult(result);
        }

        public void Release()
        {
            completion.TrySetResult(result);
        }
    }
}
