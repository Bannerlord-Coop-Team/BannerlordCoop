using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticNodeTests
{
    [Fact]
    public async Task Baseline_UsesRealLiteNetLibWithTwoPeers()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        try
        {
            using var serverOutput = new StringWriter();
            Task<int> server = DedicatedServerSyntheticNodeCommand.RunAsync(
                ServerArguments(port, variable, "baseline", expectedClients: 2),
                serverOutput,
                CancellationToken.None);
            await Task.Delay(100);

            using var clientAOutput = new StringWriter();
            using var clientBOutput = new StringWriter();
            Task<int> clientA = DedicatedServerSyntheticNodeCommand.RunAsync(
                ClientArguments(port, variable, "baseline", "ds-synthetic-client-a"),
                clientAOutput,
                CancellationToken.None);
            Task<int> clientB = DedicatedServerSyntheticNodeCommand.RunAsync(
                ClientArguments(port, variable, "baseline", "ds-synthetic-client-b"),
                clientBOutput,
                CancellationToken.None);

            int[] exitCodes = await Task.WhenAll(server, clientA, clientB);

            Assert.All(exitCodes, exitCode => Assert.Equal(0, exitCode));
            DedicatedServerSyntheticNodeResult clientResult = ParseLastResult(clientAOutput.ToString());
            Assert.Equal(1, clientResult.HeartbeatsObserved);
            Assert.Equal(1, clientResult.SessionLobbyChangesObserved);
            Assert.Equal(1, clientResult.ModuleMatchesObserved);
            Assert.Equal(0, clientResult.ModuleDenialsObserved);
            Assert.Equal(1, clientResult.FreshControllerResultsObserved);
            Assert.True(clientResult.ProtocolShortcut);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task ModuleMismatch_IsRejectedWithoutSendingClientValidation()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        try
        {
            using var serverOutput = new StringWriter();
            Task<int> server = DedicatedServerSyntheticNodeCommand.RunAsync(
                ServerArguments(port, variable, "module-mismatch", expectedClients: 1),
                serverOutput,
                CancellationToken.None);
            await Task.Delay(100);

            using var clientOutput = new StringWriter();
            Task<int> client = DedicatedServerSyntheticNodeCommand.RunAsync(
                ClientArguments(port, variable, "module-mismatch", "ds-synthetic-client-a"),
                clientOutput,
                CancellationToken.None);

            int[] exitCodes = await Task.WhenAll(server, client);

            Assert.All(exitCodes, exitCode => Assert.Equal(0, exitCode));
            DedicatedServerSyntheticNodeResult result = ParseLastResult(clientOutput.ToString());
            Assert.Equal(1, result.ModuleDenialsObserved);
            Assert.Equal(0, result.ModuleMatchesObserved);
            Assert.Equal(0, result.FreshControllerResultsObserved);
            Assert.False(result.ProtocolShortcut);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task WrongPassword_ReturnsMachineReadableRejectWithoutLeakingSecret()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        string password = Environment.GetEnvironmentVariable(variable)!;
        try
        {
            using var serverOutput = new StringWriter();
            Task<int> server = DedicatedServerSyntheticNodeCommand.RunAsync(
                ServerArguments(port, variable, "wrong-password", expectedClients: 1),
                serverOutput,
                CancellationToken.None);
            await Task.Delay(100);

            using var clientOutput = new StringWriter();
            Task<int> client = DedicatedServerSyntheticNodeCommand.RunAsync(
                ClientArguments(port, variable, "wrong-password", "ds-synthetic-client-a"),
                clientOutput,
                CancellationToken.None);

            int[] exitCodes = await Task.WhenAll(server, client);

            Assert.All(exitCodes, exitCode => Assert.Equal(0, exitCode));
            Assert.DoesNotContain(password, serverOutput.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(password, clientOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, ParseLastResult(clientOutput.ToString()).RejectedPasswords);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task DuplicateControllerIdsCannotSatisfyTwoPeerBaseline()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        try
        {
            using var serverOutput = new StringWriter();
            Task<int> server = DedicatedServerSyntheticNodeCommand.RunAsync(
                ServerArguments(port, variable, "baseline", expectedClients: 2),
                serverOutput,
                CancellationToken.None);
            await Task.Delay(100);

            using var clientAOutput = new StringWriter();
            using var duplicateOutput = new StringWriter();
            Task<int> clientA = DedicatedServerSyntheticNodeCommand.RunAsync(
                ClientArguments(port, variable, "baseline", "ds-synthetic-client-a"),
                clientAOutput,
                CancellationToken.None);
            Task<int> duplicate = DedicatedServerSyntheticNodeCommand.RunAsync(
                ClientArguments(port, variable, "baseline", "ds-synthetic-client-a"),
                duplicateOutput,
                CancellationToken.None);

            int[] exitCodes = await Task.WhenAll(server, clientA, duplicate);

            Assert.Contains(exitCodes, exitCode => exitCode != 0);
            DedicatedServerSyntheticNodeResult serverResult = ParseLastResult(serverOutput.ToString());
            Assert.False(serverResult.Success);
            Assert.Contains("invalid-client-wire-frame", serverResult.FailureCodes);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task HeldClientsRemainConnectedUntilTheControllerReleasesThem()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        try
        {
            using var serverOutput = new StringWriter();
            Task<int> server = DedicatedServerSyntheticNodeCommand.RunAsync(
                ServerArguments(port, variable, "baseline", expectedClients: 2),
                serverOutput,
                CancellationToken.None);
            await Task.Delay(100);

            var clientA = new DedicatedServerSyntheticClientNode(
                DedicatedServerSyntheticNodeOptions.Parse(
                    ClientArguments(port, variable, "baseline", "ds-synthetic-client-a")),
                holdConnection: true);
            var clientB = new DedicatedServerSyntheticClientNode(
                DedicatedServerSyntheticNodeOptions.Parse(
                    ClientArguments(port, variable, "baseline", "ds-synthetic-client-b")),
                holdConnection: true);
            Task<DedicatedServerSyntheticNodeResult> clientATask =
                clientA.RunAsync(CancellationToken.None);
            Task<DedicatedServerSyntheticNodeResult> clientBTask =
                clientB.RunAsync(CancellationToken.None);

            bool[] ready = await Task.WhenAll(clientA.Ready, clientB.Ready)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.All(ready, value => Assert.True(value));
            Assert.False(clientATask.IsCompleted);
            Assert.False(clientBTask.IsCompleted);

            clientA.Release();
            clientB.Release();
            DedicatedServerSyntheticNodeResult[] clients = await Task.WhenAll(
                clientATask,
                clientBTask);
            int serverExitCode = await server;

            Assert.Equal(0, serverExitCode);
            Assert.All(clients, client => Assert.True(client.Success));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task Release_StopsAClientBeforeTheProtocolOutcomeCompletes()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        try
        {
            var client = new DedicatedServerSyntheticClientNode(
                DedicatedServerSyntheticNodeOptions.Parse(
                    ClientArguments(port, variable, "baseline", "ds-synthetic-client-a")),
                holdConnection: true);

            Task<DedicatedServerSyntheticNodeResult> clientTask =
                client.RunAsync(CancellationToken.None);
            client.Release();
            DedicatedServerSyntheticNodeResult result = await clientTask.WaitAsync(
                TimeSpan.FromSeconds(2));

            Assert.False(result.Success);
            Assert.DoesNotContain("client-timeout", result.FailureCodes);
            Assert.DoesNotContain("client-cancelled", result.FailureCodes);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task ReadyOutputFailureReleasesTheServerSocket()
    {
        int port = FindAvailablePort();
        string variable = SetPassword();
        try
        {
            using var output = new ThrowOnceTextWriter();

            int exitCode = await DedicatedServerSyntheticNodeCommand.RunAsync(
                ServerArguments(port, variable, "baseline", expectedClients: 2),
                output,
                CancellationToken.None);

            Assert.Equal(DedicatedServerSyntheticNodeCommand.NodeFailureExitCode, exitCode);
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    private static string[] ServerArguments(
        int port,
        string passwordVariable,
        string scenario,
        int expectedClients)
    {
        var arguments = new List<string>
        {
            "--role", "server",
            "--scenario", scenario,
            "--port", port.ToString(),
            "--timeout-ms", "5000",
            "--run-token", "run-token",
            "--request-id", "request-id",
            "--expected-clients", expectedClients.ToString(),
            "--password-env", passwordVariable
        };
        AddModuleContract(arguments, scenario);
        return arguments.ToArray();
    }

    private static string[] ClientArguments(
        int port,
        string passwordVariable,
        string scenario,
        string controllerId)
    {
        var arguments = new List<string>
        {
            "--role", "client",
            "--scenario", scenario,
            "--port", port.ToString(),
            "--timeout-ms", "5000",
            "--run-token", "run-token",
            "--request-id", "request-id",
            "--controller-id", controllerId,
            "--password-env", passwordVariable
        };
        AddModuleContract(arguments, scenario);
        return arguments.ToArray();
    }

    private static void AddModuleContract(List<string> arguments, string scenario)
    {
        if (scenario == DedicatedServerSyntheticNodeOptions.WrongPasswordScenario)
        {
            return;
        }

        arguments.Add("--module-contract");
        arguments.Add(DedicatedServerSyntheticNodeOptions.EncodeModuleValidationContract(
            DedicatedServerWireCodecTests.ModuleContract()));
    }

    private static DedicatedServerSyntheticNodeResult ParseLastResult(string output)
    {
        string line = output.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Last();
        return JsonSerializer.Deserialize<DedicatedServerSyntheticNodeResult>(
            line,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static int FindAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static string SetPassword()
    {
        string variable = "DS_SYNTHETIC_NODE_PASSWORD_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "secret-" + Guid.NewGuid().ToString("N"));
        return variable;
    }

    private sealed class ThrowOnceTextWriter : StringWriter
    {
        private bool shouldThrow = true;

        public override Task WriteLineAsync(string? value)
        {
            if (shouldThrow)
            {
                shouldThrow = false;
                throw new IOException("synthetic output failure");
            }

            return base.WriteLineAsync(value);
        }
    }
}
