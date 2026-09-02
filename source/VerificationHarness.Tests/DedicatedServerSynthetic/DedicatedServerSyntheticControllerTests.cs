using Common.LiveTesting;
using System.Text.Json;
using VerificationHarness.DedicatedServerSynthetic;
using VerificationHarness.Serialization;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticControllerTests
{
    [Fact]
    public async Task ValidPreflightStillCannotProducePassedVerdict()
    {
        string response = LiveTestProtocol.SerializeResponse(new LiveTestResponse
        {
            Id = "request-id",
            Ok = true,
            Process = new LiveTestProcessInfo
            {
                Pid = 1234,
                Role = "server",
                RunToken = "run-token"
            },
            Result = new
            {
                serving = true,
                joinPort = 4201,
                connectionRoster = new[]
                {
                    new { controllerId = "ds-synthetic-client-a", connectionInstanceId = "connection-a-1", connected = true, joinState = "ResolveCharacterState" },
                    new { controllerId = "ds-synthetic-client-b", connectionInstanceId = "connection-b-1", connected = true, joinState = "ResolveCharacterState" }
                }
            }
        });
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient(response),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher());
        using var output = new StringWriter();

        int exitCode = await controller.RunAsync(ValidArguments(), output, CancellationToken.None);

        Assert.Equal(DedicatedServerSyntheticController.BlockedExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal("blocked", root.GetProperty("verdict").GetString());
        Assert.Equal("0x0000000000000011", root.GetProperty("seed").GetString());
        Assert.False(root.GetProperty("requiredChecks").GetProperty("runtime-scenario-executed").GetBoolean());
        Assert.Contains(
            root.GetProperty("failures").EnumerateArray(),
            x => x.GetString() == "runtime-scenario-controller-not-implemented");
    }

    [Fact]
    public async Task EvidencePersistenceFailureIsReportedBeforeStdout()
    {
        var controller = new DedicatedServerSyntheticController(
            new StubControlClient("{}"),
            new DedicatedServerControlResponseValidator(),
            new CanonicalJsonHasher());
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
                "--password-env", variable
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
                "--password-env", variable
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
            "--seed", "17"
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
}
