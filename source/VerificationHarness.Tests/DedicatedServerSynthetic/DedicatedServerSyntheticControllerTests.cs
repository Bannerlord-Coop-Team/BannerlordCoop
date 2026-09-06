using Common.LiveTesting;
using System.Text.Json;
using VerificationHarness.DedicatedServerSynthetic;
using VerificationHarness.Serialization;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticControllerTests
{
    [Fact]
    public async Task CompletedRuntimeScenarioProducesPassedVerdict()
    {
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient("{}"),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher(),
            new StubScenarioRunner(PassedScenario()),
            new StubArtifactVerifier(VerifiedArtifacts()));
        using var output = new StringWriter();

        int exitCode = await controller.RunAsync(ValidArguments(), output, CancellationToken.None);

        Assert.Equal(0, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal("passed", root.GetProperty("verdict").GetString());
        Assert.Equal("0x0000000000000011", root.GetProperty("seed").GetString());
        Assert.Equal(new string('a', 40), root.GetProperty("coopSource").GetProperty("head").GetString());
        Assert.True(root.GetProperty("requiredChecks").GetProperty("runtime-artifact-manifest-match").GetBoolean());
        Assert.Equal(
            new string('9', 64),
            root.GetProperty("artifactHashes").GetProperty("runtime-artifact-manifest").GetString());
        Assert.True(root.GetProperty("requiredChecks").GetProperty("runtime-scenario-executed").GetBoolean());
        Assert.Empty(root.GetProperty("failures").EnumerateArray());
    }

    [Fact]
    public async Task EvidencePersistenceFailureIsReportedBeforeStdout()
    {
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient("{}"),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher(),
            new StubScenarioRunner(PassedScenario()),
            new StubArtifactVerifier(VerifiedArtifacts()));
        using var output = new StringWriter();
        string[] arguments = ValidArguments()
            .Concat(new[] { "--output", "invalid\0path" })
            .ToArray();

        int exitCode = await controller.RunAsync(arguments, output, CancellationToken.None);

        Assert.Equal(DedicatedServerSyntheticController.BlockedExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("requiredChecks").GetProperty("evidence-output-persisted").GetBoolean());
        Assert.Contains(
            root.GetProperty("failures").EnumerateArray(),
            x => x.GetString() == "evidence-output-persistence-failed");

        using var baselineOutput = new StringWriter();
        await controller.RunAsync(ValidArguments(), baselineOutput, CancellationToken.None);
        using JsonDocument baselineDocument = JsonDocument.Parse(baselineOutput.ToString());
        Assert.NotEqual(
            baselineDocument.RootElement.GetProperty("stateDigest").GetString(),
            root.GetProperty("stateDigest").GetString());
    }

    [Fact]
    public async Task ArtifactFailureSkipsScenarioAndDoesNotEchoCallerSource()
    {
        var scenarioRunner = new StubScenarioRunner(PassedScenario());
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient("{}"),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher(),
            scenarioRunner,
            new StubArtifactVerifier(new DedicatedServerSyntheticArtifactVerification
            {
                FailureCodes = new List<string> { "artifact-manifest-invalid" }
            }));
        using var output = new StringWriter();

        int exitCode = await controller.RunAsync(
            ValidArguments(),
            output,
            CancellationToken.None);

        Assert.Equal(DedicatedServerSyntheticController.BlockedExitCode, exitCode);
        Assert.Equal(0, scenarioRunner.RunCount);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(string.Empty, root.GetProperty("coopSource").GetProperty("head").GetString());
        Assert.False(
            root.GetProperty("requiredChecks")
                .GetProperty("runtime-artifact-manifest-match")
                .GetBoolean());
        Assert.Contains(
            root.GetProperty("failures").EnumerateArray(),
            item => item.GetString() == "artifact-manifest-invalid");
    }

    [Fact]
    public async Task LifecycleWithoutControlValidationCannotPassRequiredChecks()
    {
        DedicatedServerSyntheticScenarioResult scenario = PassedScenario();
        foreach (DedicatedServerSyntheticLifecycleSnapshot snapshot in scenario.Lifecycle)
        {
            snapshot.ControlEnvelopeValidated = false;
            snapshot.ControlRequestIdentityValidated = false;
            snapshot.DedicatedProcessIdentityValidated = false;
            snapshot.FirstClassConnectionRosterValidated = false;
        }
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient("{}"),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher(),
            new StubScenarioRunner(scenario),
            new StubArtifactVerifier(VerifiedArtifacts()));
        using var output = new StringWriter();

        int exitCode = await controller.RunAsync(ValidArguments(), output, CancellationToken.None);

        Assert.Equal(DedicatedServerSyntheticController.BlockedExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement checks = document.RootElement.GetProperty("requiredChecks");
        Assert.False(checks.GetProperty("control-envelope").GetBoolean());
        Assert.False(checks.GetProperty("control-request-identity").GetBoolean());
        Assert.False(checks.GetProperty("dedicated-process-identity").GetBoolean());
        Assert.False(checks.GetProperty("first-class-connection-roster").GetBoolean());
    }

    [Fact]
    public async Task ProcessRestartBetweenAttestationsFailsClosed()
    {
        DedicatedServerSyntheticArtifactVerification preflight = VerifiedArtifacts();
        DedicatedServerSyntheticArtifactVerification postflight = VerifiedArtifacts(
            preflight.ProcessStartedUtc.AddSeconds(1));
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient("{}"),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher(),
            new StubScenarioRunner(PassedScenario()),
            new StubArtifactVerifier(preflight, postflight));
        using var output = new StringWriter();

        int exitCode = await controller.RunAsync(
            ValidArguments(),
            output,
            CancellationToken.None);

        Assert.Equal(DedicatedServerSyntheticController.BlockedExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Contains(
            document.RootElement.GetProperty("failures").EnumerateArray(),
            item => item.GetString() == "dedicated-server-process-changed");
    }

    [Fact]
    public void PasswordRedaction_RemovesEveryOccurrenceAndCapsEvidenceText()
    {
        const string password = "correct horse battery staple";
        string input = $"failure '{password}' repeated {password}" + new string('x', 5000);

        string result = DedicatedServerSecretRedactor.Redact(input, password);

        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.Equal(2, result.Split(DedicatedServerSecretRedactor.Marker).Length - 1);
        Assert.True(result.Length <= 4096);
    }

    [Fact]
    public void NodeOptions_DoNotExposePasswordThroughPublicJson()
    {
        string variable = "DS_SYNTHETIC_TEST_PASSWORD_" + Guid.NewGuid().ToString("N");
        const string password = "never-print-this-password";
        Environment.SetEnvironmentVariable(variable, password);
        try
        {
            DedicatedServerSyntheticNodeOptions options = DedicatedServerSyntheticNodeOptions.Parse(new[]
            {
                "--role", "client",
                "--scenario", "baseline",
                "--port", "4201",
                "--timeout-ms", "1000",
                "--run-token", "run-token",
                "--request-id", "request-id",
                "--controller-id", "ds-synthetic-client-a",
                "--password-env", variable,
                "--module-contract", DedicatedServerSyntheticNodeOptions.EncodeModuleValidationContract(
                    DedicatedServerWireCodecTests.ModuleContract())
            });

            string json = JsonSerializer.Serialize(options);
            Assert.DoesNotContain(password, json, StringComparison.Ordinal);
            Assert.Contains(variable, json, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void NodeOptions_RequireTheFixedTwoClientBaselineTopology()
    {
        string variable = "DS_SYNTHETIC_TEST_PASSWORD_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "password");
        try
        {
            string[] arguments =
            {
                "--role", "server",
                "--scenario", "baseline",
                "--port", "4201",
                "--timeout-ms", "1000",
                "--run-token", "run-token",
                "--request-id", "request-id",
                "--expected-clients", "1",
                "--password-env", variable,
                "--module-contract", DedicatedServerSyntheticNodeOptions.EncodeModuleValidationContract(
                    DedicatedServerWireCodecTests.ModuleContract())
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                DedicatedServerSyntheticNodeOptions.Parse(arguments));
            Assert.Contains("--expected-clients 2", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    private static string[] ValidArguments()
    {
        return new[]
        {
            "--head", new string('a', 40),
            "--tree", new string('b', 40),
            "--server-head", new string('c', 40),
            "--server-tree", new string('d', 40),
            "--server-pid", "1234",
            "--run-token", "run-token",
            "--request-id", "request-id",
            "--join-port", "4201",
            "--timeout-ms", "1000",
            "--seed", "17",
            "--artifact-manifest", "staged-artifacts.json",
            "--artifact-manifest-sha256", new string('7', 64),
            "--artifact-root", "staged-runtime",
            "--password-env", SetControllerPassword()
        };
    }

    private static string SetControllerPassword()
    {
        const string variable = "DS_SYNTHETIC_CONTROLLER_PASSWORD";
        Environment.SetEnvironmentVariable(variable, "controller-test-password");
        return variable;
    }

    private static DedicatedServerSyntheticScenarioResult PassedScenario()
    {
        DedicatedServerRosterEntry Client(string controllerId, string connectionId) =>
            new(controllerId, connectionId, true, "CreateCharacterState");
        DedicatedServerSyntheticLifecycleSnapshot Snapshot(
            string phase,
            params DedicatedServerRosterEntry[] roster) => new()
            {
                Phase = phase,
                ControlEnvelopeValidated = true,
                ControlRequestIdentityValidated = true,
                DedicatedProcessIdentityValidated = true,
                FirstClassConnectionRosterValidated = true,
                Serving = true,
                JoinPort = 4201,
                ConnectionRoster = roster.ToList()
            };

        return new DedicatedServerSyntheticScenarioResult
        {
            Attempted = true,
            Completed = true,
            WrongPasswordRejected = true,
            IncompatibleModuleRejected = true,
            CompatibleModuleHandshakeCompleted = true,
            ProtocolShortcut = true,
            ModuleValidation = new DedicatedModuleValidationContract(
                "1.2.3+test",
                Array.Empty<DedicatedModuleInfo>()),
            Lifecycle = new List<DedicatedServerSyntheticLifecycleSnapshot>
            {
                Snapshot("before-connect"),
                Snapshot(
                    "two-connected",
                    Client("ds-synthetic-client-a", "connection-a-1"),
                    Client("ds-synthetic-client-b", "connection-b-1")),
                Snapshot(
                    "one-disconnected",
                    Client("ds-synthetic-client-b", "connection-b-1")),
                Snapshot(
                    "reconnected",
                    Client("ds-synthetic-client-a", "connection-a-2"),
                    Client("ds-synthetic-client-b", "connection-b-1")),
                Snapshot("final-empty")
            },
            Clients = Enumerable.Range(0, 5)
                .Select(index => new DedicatedServerSyntheticNodeResult
                {
                    RequestId = "client-" + index,
                    Success = true,
                    ProtocolShortcut = index > 0
                })
                .ToList(),
            WireHashes = new List<string> { new string('e', 64) }
        };
    }

    private static DedicatedServerSyntheticArtifactVerification VerifiedArtifacts(
        DateTime? processStartedUtc = null)
    {
        return new DedicatedServerSyntheticArtifactVerification
        {
            RuntimeArtifactsMatch = true,
            ManifestFileSha256 = new string('7', 64),
            ProcessStartedUtc = processStartedUtc ??
                new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            Manifest = new DedicatedServerSyntheticArtifactManifest
            {
                CoopSource = new DedicatedServerSyntheticSourceIdentity
                {
                    Head = new string('a', 40),
                    Tree = new string('b', 40)
                },
                DedicatedServerSource = new DedicatedServerSyntheticSourceIdentity
                {
                    Head = new string('c', 40),
                    Tree = new string('d', 40)
                },
                ArtifactSetDigest = new string('8', 64),
                ManifestDigest = new string('9', 64)
            }
        };
    }

    private sealed class StubControlClient : IDedicatedServerControlClient
    {
        private readonly string response;

        public StubControlClient(string response)
        {
            this.response = response;
        }

        public Task<string> GetStatusAsync(
            int processId,
            string requestId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class StubScenarioRunner : IDedicatedServerSyntheticScenarioRunner
    {
        private readonly DedicatedServerSyntheticScenarioResult result;

        public StubScenarioRunner(DedicatedServerSyntheticScenarioResult result)
        {
            this.result = result;
        }

        public Task<DedicatedServerSyntheticScenarioResult> RunAsync(
            DedicatedServerSyntheticOptions options,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(result);
        }

        public int RunCount { get; private set; }
    }

    private sealed class StubArtifactVerifier : IDedicatedServerSyntheticArtifactVerifier
    {
        private readonly Queue<DedicatedServerSyntheticArtifactVerification> verifications;

        public StubArtifactVerifier(params DedicatedServerSyntheticArtifactVerification[] verifications)
        {
            this.verifications = new Queue<DedicatedServerSyntheticArtifactVerification>(
                verifications);
        }

        public Task<DedicatedServerSyntheticArtifactVerification> VerifyAsync(
            DedicatedServerSyntheticOptions options,
            CancellationToken cancellationToken)
        {
            DedicatedServerSyntheticArtifactVerification verification =
                verifications.Count > 1 ? verifications.Dequeue() : verifications.Peek();
            return Task.FromResult(verification);
        }
    }
}
