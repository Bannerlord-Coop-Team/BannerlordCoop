using System.Diagnostics;
using System.Text.Json;

namespace VerificationHarness.Tests.PeerHost;

public sealed class PeerProcessLifecycleTests
{
    [Fact]
    [Trait("Category", "ProcessIsolationFoundation")]
    public async Task TwoHostsKeepStateAndSequencesProcessLocal()
    {
        await using var first = await PeerHostProcess.Start("client-a");
        await using var second = await PeerHostProcess.Start("client-b");

        JsonElement firstHello = await first.Send(1, "hello");
        JsonElement secondHello = await second.Send(1, "hello");

        Assert.Equal("client-a", firstHello.GetProperty("instanceId").GetString());
        Assert.Equal("client-b", secondHello.GetProperty("instanceId").GetString());
        Assert.NotEqual(
            firstHello.GetProperty("processId").GetInt32(),
            secondHello.GetProperty("processId").GetInt32());

        await first.Send(2, "put", new { key = "marker", value = "first" });
        JsonElement missingFromSecond = await second.Send(2, "get", new { key = "marker" });
        Assert.False(missingFromSecond.GetProperty("result").GetProperty("found").GetBoolean());

        JsonElement presentInFirst = await first.Send(3, "get", new { key = "marker" });
        Assert.True(presentInFirst.GetProperty("result").GetProperty("found").GetBoolean());
        Assert.Equal("first", presentInFirst.GetProperty("result").GetProperty("value").GetString());

        await second.Send(3, "shutdown");
        await first.Send(4, "shutdown");

        Assert.Equal(0, await second.WaitForExit());
        Assert.Equal(0, await first.WaitForExit());
    }

    [Fact]
    [Trait("Category", "ProcessIsolationFoundation")]
    public async Task EquivalentHostsConvergeAndCounterDivergenceIsExplainable()
    {
        await using var first = await PeerHostProcess.Start("replica");
        await using var second = await PeerHostProcess.Start("replica");

        JsonElement firstHello = await first.Send(1, "hello");
        JsonElement secondHello = await second.Send(1, "hello");
        Assert.NotEqual(
            firstHello.GetProperty("processId").GetInt32(),
            secondHello.GetProperty("processId").GetInt32());

        JsonElement firstSnapshot = await first.Send(2, "snapshot");
        JsonElement secondSnapshot = await second.Send(2, "snapshot");
        string firstDigest = firstSnapshot.GetProperty("result").GetProperty("digest").GetString()!;
        string secondDigest = secondSnapshot.GetProperty("result").GetProperty("digest").GetString()!;
        Assert.Equal(firstDigest, secondDigest);

        await first.Send(3, "ping");
        await second.Send(3, "get", new { key = "missing" });

        JsonElement divergedFirst = await first.Send(4, "snapshot");
        JsonElement divergedSecond = await second.Send(4, "snapshot");
        JsonElement firstResult = divergedFirst.GetProperty("result");
        JsonElement secondResult = divergedSecond.GetProperty("result");
        Assert.NotEqual(
            firstResult.GetProperty("digest").GetString(),
            secondResult.GetProperty("digest").GetString());
        Assert.Equal(1, firstResult.GetProperty("fields").GetProperty("counters").GetProperty("ping").GetInt64());
        Assert.Equal(0, secondResult.GetProperty("fields").GetProperty("counters").GetProperty("ping").GetInt64());

        await first.Send(5, "shutdown");
        await second.Send(5, "shutdown");
        Assert.Equal(0, await first.WaitForExit());
        Assert.Equal(0, await second.WaitForExit());
    }

    private sealed class PeerHostProcess : IAsyncDisposable
    {
        private readonly Process process;

        private PeerHostProcess(Process process)
        {
            this.process = process;
        }

        public static async Task<PeerHostProcess> Start(string instanceId)
        {
            string testAssembly = typeof(PeerProcessLifecycleTests).Assembly.Location;
            string runtimeConfig = Path.ChangeExtension(testAssembly, ".runtimeconfig.json");
            string dependencyManifest = Path.ChangeExtension(testAssembly, ".deps.json");
            string hostAssembly = typeof(VerificationHarness.Program).Assembly.Location;
            string dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

            var startInfo = new ProcessStartInfo
            {
                FileName = dotnetHost,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--runtimeconfig");
            startInfo.ArgumentList.Add(runtimeConfig);
            startInfo.ArgumentList.Add("--depsfile");
            startInfo.ArgumentList.Add(dependencyManifest);
            startInfo.ArgumentList.Add(hostAssembly);
            startInfo.ArgumentList.Add("peer-host");
            startInfo.ArgumentList.Add("--instance-id");
            startInfo.ArgumentList.Add(instanceId);

            var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Failed to start peer host process.");
            }

            return new PeerHostProcess(process);
        }

        public async Task<JsonElement> Send(long sequence, string command, object? payload = null)
        {
            string request = JsonSerializer.Serialize(new
            {
                protocolVersion = 1,
                sequence,
                command,
                payload
            });
            await process.StandardInput.WriteLineAsync(request);
            await process.StandardInput.FlushAsync();

            string? line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (line == null)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Peer host exited without a response: {error}");
            }

            using JsonDocument document = JsonDocument.Parse(line);
            return document.RootElement.Clone();
        }

        public async Task<int> WaitForExit()
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return process.ExitCode;
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.StandardInput.Close();
                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }

            process.Dispose();
        }
    }
}
